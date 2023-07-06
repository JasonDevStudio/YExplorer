using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using Newtonsoft.Json;

namespace YExplorer;

public partial class MainViewModel : ObservableObject
{
    private static List<string> picExt = new List<string> { ".jpg", ".png", ".gif", ".bmp" };

    private static List<string> videoExt = new List<string>
        { ".mp4", ".avi", ".mkv", ".rmvb", ".wmv", ".ts", ".m4v", ".mov", ".flv" };

    private static List<string> storeExt = new List<string> { ".aria2", ".torrent" };

    private decimal oneMbSize = 2 * 1024 * 1024;
    private decimal videoMaxMbSize = 100 * 1024 * 1024;
    private string dirPath;
    private ObservableCollection<VideoEnty> videos = new ObservableCollection<VideoEnty>();
    private string log;
    private List<FileInfo> videoFiles = new List<FileInfo>();
    private SynchronizedCollection<VideoEnty> videoCollection = new SynchronizedCollection<VideoEnty>();

    public string DirPath
    {
        get => this.dirPath;
        set => this.SetProperty(ref this.dirPath, value);
    }

    public ObservableCollection<VideoEnty> Videos
    {
        get => this.videos;
        set => this.SetProperty(ref this.videos, value);
    }

    public string Log
    {
        get => this.log;
        set => this.SetProperty(ref this.log, value);
    }

    [RelayCommand]
    public async Task ProcessForDirAsync()
    {
        this.videoFiles?.Clear();
        var uri = this.DirPath;
        var dirInfo = new DirectoryInfo(uri);
        await this.ProcessForDirsAsync(dirInfo);
        await this.ProcessVideosAsync(dirInfo.Name);
        this.Videos = new ObservableCollection<VideoEnty>(this.videoCollection);
    }

    [RelayCommand]
    public async Task DeleteAllAsync()
    {
        var uri = this.DirPath;
        var dirInfo = new DirectoryInfo(uri);
        await Task.Run(() => DeleteAll(dirInfo));
    }

    [RelayCommand]
    public async Task LoadDirAsync()
    {
        this.Videos.Clear();
        var uri = this.DirPath;
        var dirInfo = new DirectoryInfo(uri);
        var _videos = await this.LoadDirAsync(dirInfo);
        this.Videos = new ObservableCollection<VideoEnty>(_videos.OrderByDescending(m => m.MidifyTime));
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        var uri = this.DirPath;
        var dirInfo = new DirectoryInfo(uri);
        await Task.Run(() => this.Save(dirInfo));
    }

    [RelayCommand]
    public void Play(object param)
    {
        string path = param as string;
        if (!string.IsNullOrWhiteSpace(path))
        {
            Process.Start(@"C:\Program Files\DAUM\PotPlayer\PotPlayerMini64.exe", path);

            var video = this.Videos.FirstOrDefault(m => m.VideoPath == path);
            video.PlayCount++;
            this.SaveAsync();

        }
    }

    [RelayCommand]
    public void Folder(object param)
    {
        string path = param as string;
        var dirPath = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(path))
        {
            Process.Start("explorer.exe", dirPath);
        }
    }

    [RelayCommand]
    public void Delete(object param)
    {
        if (param is VideoEnty video)
        {
            File.Delete(video.VideoPath);
            File.Delete(video.SnapshotPath1);
            File.Delete(video.SnapshotPath2);
            File.Delete(video.SnapshotPath3);
            File.Delete(video.SnapshotPath4);
            File.Delete(video.SnapshotPath5);

            this.Videos.Remove(video);
        }
    }

    private void DeleteAll(DirectoryInfo dirInfo)
    {
        this.Output($"Start del {this.DirPath} ...");
        var files = dirInfo.GetFiles();
        var picFiles = files.Where(f => picExt.Contains(f.Extension.ToLower())).ToList();
        var videoFiles = files.Where(f => videoExt.Contains(f.Extension.ToLower())).ToList();
        var videoDelFiles = videoFiles.Where(m => m.Length < videoMaxMbSize).ToList();
        var videoStoreFiles = videoFiles.Where(m => m.Length >= videoMaxMbSize).ToList();
        var picDelFiles = picFiles.Where(m => m.Length < oneMbSize).ToList();
        var otherDelFiles = files
            .Where(m => !picExt.Contains(m.Extension.ToLower()) &&
            !videoExt.Contains(m.Extension.ToLower()) &&
            !storeExt.Contains(m.Extension.ToLower())).ToList();

        if (picDelFiles?.Any() ?? false)
        {
            this.Output($"Del {dirInfo.Name} images .");
            foreach (var item in picDelFiles)
                item.Delete();
        }

        if (videoDelFiles?.Any() ?? false)
        {
            this.Output($"Del {dirInfo.Name} small videos .");
            foreach (var item in videoDelFiles)
                item.Delete();
        }

        if (otherDelFiles?.Any() ?? false)
        {
            this.Output($"Del {dirInfo.Name} other files .");
            foreach (var item in otherDelFiles)
                item.Delete();
        }

        var chidDirs = dirInfo.GetDirectories();
        if (chidDirs?.Any() ?? false)
        {
            foreach (var item in chidDirs)
            {
                DeleteAll(item);
            }
        }
        else if (!(videoStoreFiles?.Any() ?? false))
        {
            dirInfo.Delete(true);
        }

        this.Output($"End del {this.DirPath} .");
    }

    private async Task<List<VideoEnty>> LoadDirAsync(DirectoryInfo dirInfo)
    {
        this.Output($"Start Load {dirInfo.Name} ...");
        var videoEnties = new List<VideoEnty>();

        try
        {
            var jsonpath = Path.Combine(AppContext.BaseDirectory, "data", dirInfo.Name);
            var jsonfile = Path.Combine(jsonpath, $"{dirInfo.Name}.json");
            if (File.Exists(jsonfile))
            {
                var json = await File.ReadAllTextAsync(jsonfile);
                videoEnties = JsonConvert.DeserializeObject<List<VideoEnty>>(json);
                return videoEnties;
            }

            var files = dirInfo.GetFiles();
            var videoFiles = files.Where(f => videoExt.Contains(f.Extension.ToLower())).ToList();
            var chidDirs = dirInfo.GetDirectories();

            if (videoFiles?.Any() ?? false)
            {
                foreach (var video in videoFiles)
                {
                    var name = Path.GetFileNameWithoutExtension(video.Name);
                    var videoEnty = new VideoEnty();
                    var snapshotName1 = $"{name}_1.png";
                    var snapshotPath1 = Path.Combine(dirInfo.FullName, snapshotName1);
                    var snapshotName2 = $"{name}_.png";
                    var snapshotPath2 = Path.Combine(dirInfo.FullName, snapshotName2);
                    var snapshotName3 = $"{name}_3.png";
                    var snapshotPath3 = Path.Combine(dirInfo.FullName, snapshotName3);
                    var snapshotName4 = $"{name}_4.png";
                    var snapshotPath4 = Path.Combine(dirInfo.FullName, snapshotName4);
                    var snapshotName5 = $"{name}_5.png";
                    var snapshotPath5 = Path.Combine(dirInfo.FullName, snapshotName5);

                    videoEnty.Caption = Path.GetFileNameWithoutExtension(video.Name);
                    videoEnty.VideoPath = video.FullName;
                    videoEnty.SnapshotPath1 = snapshotPath1;
                    videoEnty.SnapshotPath2 = snapshotPath2;
                    videoEnty.SnapshotPath3 = snapshotPath3;
                    videoEnty.SnapshotPath4 = snapshotPath4;
                    videoEnty.SnapshotPath5 = snapshotPath5;
                    videoEnties.Add(videoEnty);
                }
            }

            if (chidDirs?.Any() ?? false)
            {
                foreach (var item in chidDirs)
                {
                    var enties = await this.LoadDirAsync(item);
                    if (enties?.Any() ?? false)
                        videoEnties.AddRange(enties);
                }
            }
        }
        catch (Exception ex)
        {
            this.Output($"Error: {dirInfo.FullName}{Environment.NewLine}{ex}");
            MessageBox.Show($"Error: {dirInfo.FullName}{Environment.NewLine}{ex}");
        }

        this.Output($"End Load {dirInfo.Name} ...");
        return videoEnties;
    }

    private async Task ProcessForDirsAsync(DirectoryInfo dirInfo)
    {
        this.Output($"Start Process {this.DirPath} ...");

        var files = dirInfo.GetFiles();
        var videoFiles = files.Where(f => videoExt.Contains(f.Extension.ToLower())).ToList();
        var videoStoreFiles = videoFiles.Where(m => m.Length >= videoMaxMbSize).ToList();
        if (videoStoreFiles?.Any() ?? false)
        {
            this.Output($"Global video files added .");

            lock (this.videoFiles)
                this.videoFiles.AddRange(videoStoreFiles);
        }

        var chidDirs = dirInfo.GetDirectories();
        if (chidDirs?.Any() ?? false)
        {
            foreach (var item in chidDirs)
            {
                await ProcessForDirsAsync(item);
            }
        }

        this.Output($"End Process {this.DirPath} .");
    }

    private async Task ProcessVideosAsync(string dirName)
    {
        var taskCount = 8;
        var batchSize = this.videoFiles.Count / taskCount;
        batchSize = batchSize <= 0 ? 1 : batchSize;

        var array = this.videoFiles.Chunk(batchSize).ToList();
        var jsonpath = Path.Combine(AppContext.BaseDirectory, "data", dirName);
        var tasks = new List<Task>(taskCount);

        if (Directory.Exists(jsonpath))
            Directory.Delete(jsonpath, true);

        Directory.CreateDirectory(jsonpath);

        for (int i = 0; i < array.Count; i++)
        {
            tasks.Add(Task.Factory.StartNew(async obj =>
            {
                var videos = obj as FileInfo[];
                await ProcessVideosAsync(dirName, videos);
            }, array[i]));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProcessVideosAsync(string dirName, FileInfo[] fileInfos)
    {
        using var libVLC = new LibVLC();
        using var mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(libVLC);
        var jsonpath = Path.Combine(AppContext.BaseDirectory, "data", dirName);

        foreach (var item in fileInfos)
        {
            try
            {
                var media = new Media(libVLC, item.FullName, FromType.FromPath);
                mediaPlayer.Media = media;
                mediaPlayer.Play();

                while (mediaPlayer.State != VLCState.Playing)
                    Thread.Sleep(1000);

                var videoEnty = new VideoEnty();
                videoEnty.Caption = Path.GetFileNameWithoutExtension(item.Name);
                videoEnty.Length = item.Length / 1024 / 1024;
                videoEnty.VideoPath = item.FullName;
                videoEnty.MidifyTime = item.LastWriteTime;

                for (int i = 0; i < 5; i++)
                {
                    mediaPlayer.Time = (4 + i) * 60 * 1000;
                    await Task.Delay(1000);
                    var picName = $"{Guid.NewGuid()}.png";
                    var snapshotPath = Path.Combine(jsonpath, picName);

                    if (i == 0)
                        videoEnty.SnapshotPath1 = snapshotPath;

                    if (i == 1)
                        videoEnty.SnapshotPath2 = snapshotPath;

                    if (i == 2)
                        videoEnty.SnapshotPath3 = snapshotPath;

                    if (i == 3)
                        videoEnty.SnapshotPath4 = snapshotPath;

                    if (i == 4)
                        videoEnty.SnapshotPath5 = snapshotPath;

                    if (File.Exists(snapshotPath))
                        File.Delete(snapshotPath);

                    mediaPlayer.TakeSnapshot(0, snapshotPath, 0, 0);
                    await Task.Delay(1000);
                    this.Output($"Snapshot {snapshotPath} .");
                }

                this.videoCollection.Add(videoEnty);
            }
            catch (Exception ex)
            {
                this.Output($"Error: {item.FullName}{Environment.NewLine}{ex}");
            }
        }

        mediaPlayer.Dispose();
    }

    private void Save(DirectoryInfo dirInfo)
    {
        var jsonpath = Path.Combine(AppContext.BaseDirectory, "data", dirInfo.Name);

        if (!Directory.Exists(jsonpath))
            Directory.CreateDirectory(jsonpath);

        //foreach (var item in this.Videos)
        //{
        //    var pic1 = Path.Combine(jsonpath, $"{Guid.NewGuid()}.png");
        //    var pic2 = Path.Combine(jsonpath, $"{Guid.NewGuid()}.png");
        //    var pic3 = Path.Combine(jsonpath, $"{Guid.NewGuid()}.png");
        //    var pic4 = Path.Combine(jsonpath, $"{Guid.NewGuid()}.png");
        //    var pic5 = Path.Combine(jsonpath, $"{Guid.NewGuid()}.png");

        //    if (File.Exists(item.SnapshotPath1))
        //        File.Copy(item.SnapshotPath1, pic1, true);

        //    if (File.Exists(item.SnapshotPath2))
        //        File.Copy(item.SnapshotPath2, pic2, true);

        //    if (File.Exists(item.SnapshotPath3))
        //        File.Copy(item.SnapshotPath3, pic3, true);

        //    if (File.Exists(item.SnapshotPath4))
        //        File.Copy(item.SnapshotPath4, pic4, true);

        //    if (File.Exists(item.SnapshotPath5))
        //        File.Copy(item.SnapshotPath5, pic5, true);

        //    item.SnapshotPath1 = pic1;
        //    item.SnapshotPath2 = pic2;
        //    item.SnapshotPath3 = pic3;
        //    item.SnapshotPath4 = pic4;
        //    item.SnapshotPath5 = pic5;
        //}


        var json = JsonConvert.SerializeObject(this.videoCollection, Formatting.Indented);
        var jsonfile = Path.Combine(jsonpath, $"{dirInfo.Name}.json");

        if (!Directory.Exists(jsonpath))
            Directory.CreateDirectory(jsonpath);

        if (File.Exists(jsonfile))
            File.Delete(jsonfile);

        File.WriteAllText(jsonfile, json);
    }

    private void Output(string msg)
    {
        this.Log += $"{Environment.NewLine}[{DateTime.Now:HH:mm:ss fff}] {msg} ";
    }
}

public class VideoEnty : ObservableObject
{
    private string _caption;
    private string videoPath;
    private long length;
    private long playCount = 0;
    public DateTime? midifyTime;
    private string snapshotPath1;
    private string snapshotPath2;
    private string snapshotPath3;
    private string snapshotPath4;
    private string snapshotPath5;

    public long Length
    {
        get => this.length;
        set => this.SetProperty(ref this.length, value);
    }

    public string Caption
    {
        get => this._caption;
        set => this.SetProperty(ref this._caption, value);
    }

    public string VideoPath
    {
        get => this.videoPath;
        set => this.SetProperty(ref this.videoPath, value);
    }

    public long PlayCount
    {
        get => this.playCount;
        set => this.SetProperty(ref this.playCount, value);
    }

    public DateTime? MidifyTime { get => this.midifyTime; set => this.SetProperty(ref this.midifyTime, value); }

    public string SnapshotPath1
    {
        get => this.snapshotPath1;
        set => this.SetProperty(ref this.snapshotPath1, value);
    }

    public string SnapshotPath2
    {
        get => this.snapshotPath2;
        set => this.SetProperty(ref this.snapshotPath2, value);
    }

    public string SnapshotPath3
    {
        get => this.snapshotPath3;
        set => this.SetProperty(ref this.snapshotPath3, value);
    }

    public string SnapshotPath4
    {
        get => this.snapshotPath4;
        set => this.SetProperty(ref this.snapshotPath4, value);
    }

    public string SnapshotPath5
    {
        get => this.snapshotPath5;
        set => this.SetProperty(ref this.snapshotPath5, value);
    }
}
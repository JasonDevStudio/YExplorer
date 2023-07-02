using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;

namespace YExplorer;

public partial class MainViewModel : ObservableObject
{
    private static List<string> picExt = new List<string> { ".jpg", ".png", ".gif", ".bmp" };

    private static List<string> videoExt = new List<string>
        { ".mp4", ".avi", ".mkv", ".rmvb", ".wmv", ".ts", ".m4v", ".mov", ".flv" };

    private decimal oneMbSize = 2 * 1024 * 1024;
    private decimal videoMaxMbSize = 100 * 1024 * 1024;
    private string dirPath;
    private ObservableCollection<VideoEnty> videos = new ObservableCollection<VideoEnty>();
    private string log;
    private List<FileInfo> videoFiles = new List<FileInfo>();

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
        var uri = this.DirPath;
        var dirInfo = new DirectoryInfo(uri);
        await this.ProcessForDirsAsync(dirInfo);
        await this.ProcessVideoAsync();
    }

    [RelayCommand]
    public async Task LoadDirAsync()
    {
        this.Videos.Clear();
        var uri = this.DirPath;
        var dirInfo = new DirectoryInfo(uri);
        await this.LoadDirAsync(dirInfo);
    }

    [RelayCommand]
    public void Play(object param)
    {
        string path = param as string;
        if (!string.IsNullOrWhiteSpace(path))
        {
            Process.Start(@"C:\Program Files\DAUM\PotPlayer\PotPlayerMini64.exe", path);
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
    
    private async Task LoadDirAsync(DirectoryInfo dirInfo)
    {
        this.Output($"Start Load {dirInfo.Name} ...");
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
                this.Videos.Add(videoEnty);
            }
        }

        if (chidDirs?.Any() ?? false)
        {
            foreach (var item in chidDirs)
            {
                await this.LoadDirAsync(item);
            }
        }

        this.Output($"End Load {dirInfo.Name} ...");
    }

    private async Task ProcessForDirAsync(DirectoryInfo dirInfo)
    {
        this.Output($"Start Process {this.DirPath} ...");
        var files = dirInfo.GetFiles();
        var picFiles = files.Where(f => picExt.Contains(f.Extension.ToLower())).ToList();
        var videoFiles = files.Where(f => videoExt.Contains(f.Extension.ToLower())).ToList();
        var videoDelFiles = videoFiles.Where(m => m.Length < videoMaxMbSize).ToList();
        var videoStoreFiles = videoFiles.Where(m => m.Length >= videoMaxMbSize).ToList();
        var picDelFiles = picFiles.Where(m => m.Length < oneMbSize).ToList();
        var otherDelFiles = files
            .Where(m => !picExt.Contains(m.Extension.ToLower()) && !videoExt.Contains(m.Extension.ToLower())).ToList();

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

        if (videoStoreFiles?.Any() ?? false)
        {
            this.Output($"Start vlc process {dirInfo.Name} videos .");
            foreach (var item in videoStoreFiles)
            {
                using var libVLC = new LibVLC();
                using var mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(libVLC);
                var media = new Media(libVLC, item.FullName, FromType.FromPath);

                // Wait for the media to be parsed
                await media.Parse(MediaParseOptions.ParseNetwork);

                var duration = media.Duration;
                mediaPlayer.Play(media);

                for (int i = 0; i < 5; i++)
                {
                    mediaPlayer.Time = (4 + i) * 60 * 1000;
                    await Task.Delay(2000);
                    var snapshotPath = Path.Combine(dirInfo.FullName,
                        $"Snapshot_{i + 1}.png");
                    mediaPlayer.TakeSnapshot(0, snapshotPath, 0, 0);
                    await Task.Delay(2000);
                    this.Output($"Snapshot {snapshotPath} .");
                }


                // Dispose the media
                media.Dispose();
            }

            this.Output($"End vlc process {dirInfo.Name} videos .");
        }

        var chidDirs = dirInfo.GetDirectories();
        if (chidDirs?.Any() ?? false)
        {
            foreach (var item in chidDirs)
            {
                await ProcessForDirAsync(item);
            }
        }
        else if (!(videoStoreFiles?.Any() ?? false))
        {
            dirInfo.Delete(true);
        }

        this.Output($"End Process {this.DirPath} .");
    }

    private async Task ProcessForDirsAsync(DirectoryInfo dirInfo)
    {
        this.Output($"Start Process {this.DirPath} ...");
        var files = dirInfo.GetFiles();
        var picFiles = files.Where(f => picExt.Contains(f.Extension.ToLower())).ToList();
        var videoFiles = files.Where(f => videoExt.Contains(f.Extension.ToLower())).ToList();
        var videoDelFiles = videoFiles.Where(m => m.Length < videoMaxMbSize).ToList();
        var videoStoreFiles = videoFiles.Where(m => m.Length >= videoMaxMbSize).ToList();
        var picDelFiles = picFiles.Where(m => m.Length < oneMbSize).ToList();
        var picStoreFiles = picFiles.Where(m => m.Length >= oneMbSize).ToList();
        var otherDelFiles = files
            .Where(m => !picExt.Contains(m.Extension.ToLower()) && !videoExt.Contains(m.Extension.ToLower())).ToList();

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

        if (videoStoreFiles?.Any() ?? false)
        {
            this.Output($"Global video files added .");
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
        else if (!(videoStoreFiles?.Any() ?? false) && !(picStoreFiles?.Any() ?? false))
        {
            dirInfo.Delete(true);
        }

        this.Output($"End Process {this.DirPath} .");
    }

    private async Task ProcessVideoAsync()
    {
        var taskCount = 6;
        var batchSize = this.videoFiles.Count / taskCount + 1;
        var array = this.videoFiles.Chunk(batchSize).ToList();
        var tasks = new List<Task>(taskCount);

        for (int i = 0; i < array.Count; i++)
        {
            tasks.Add(Task.Factory.StartNew(async obj =>
            {
                var videos = obj as FileInfo[];
                foreach (var item in videos)
                    await this.ProcessVideoAsync(item);
            }, array[i]));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProcessVideoAsync(FileInfo fileInfo)
    {
        using var libVLC = new LibVLC();
        using var mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(libVLC);
        var media = new Media(libVLC, fileInfo.FullName, FromType.FromPath);

        // Wait for the media to be parsed
        await media.Parse(MediaParseOptions.ParseNetwork);

        var duration = media.Duration;
        mediaPlayer.Play(media);

        for (int i = 0; i < 5; i++)
        {
            mediaPlayer.Time = (4 + i) * 60 * 1000;
            await Task.Delay(2000);
            var noExtName = Path.GetFileNameWithoutExtension(fileInfo.Name);
            var dirPath = Path.GetDirectoryName(fileInfo.FullName);
            var snapshotPath = Path.Combine(dirPath, $"{noExtName}_{i + 1}.png");

            if (File.Exists(snapshotPath))
                File.Delete(snapshotPath);

            mediaPlayer.TakeSnapshot(0, snapshotPath, 0, 0);
            await Task.Delay(1000);
            this.Output($"Snapshot {snapshotPath} .");
        }

        // Dispose the media
        media.Dispose();
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
    private string snapshotPath1;
    private string snapshotPath2;
    private string snapshotPath3;
    private string snapshotPath4;
    private string snapshotPath5;

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
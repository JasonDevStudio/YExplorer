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
using System.Windows.Controls;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using Newtonsoft.Json;

namespace YExplorer;

public partial class MainViewModel : ObservableObject
{
    public MainViewModel()
    {
        //this.ProcessDirCommand = new AsyncRelayCommand(ProcessForDirAsync);
        this.DeleteAllCommand = new AsyncRelayCommand(DeleteAllAsync);
        this.LoadDirCommand = new AsyncRelayCommand(LoadDirAsync);
        this.SaveCommand = new AsyncRelayCommand(SaveAsync);

        this.paths.Add($@"\\192.168.10.2\99_资源收藏\01_成人资源\17_1024");
        this.paths.Add($@"\\192.168.10.2\99_资源收藏\01_成人资源\16_1024");
        this.paths.Add($@"\\192.168.10.2\99_资源收藏\01_成人资源\15_1024");
    }

    #region Fields

    private static List<string> picExt = new List<string> { ".jpg", ".png", ".gif", ".bmp" };

    private static List<string> videoExt = new List<string>
        { ".mp4", ".avi", ".mkv", ".rmvb", ".wmv", ".ts", ".m4v", ".mov", ".flv" };

    private static List<string> storeExt = new List<string> { ".aria2", ".torrent" };

    private decimal oneMbSize = 2 * 1024 * 1024;
    private decimal videoMaxMbSize = 100 * 1024 * 1024;
    private string dirPath;
    private ObservableCollection<VideoEnty> videos = new ObservableCollection<VideoEnty>();
    private ObservableCollection<VideoEnty> _tmpVideos = new ObservableCollection<VideoEnty>();
    private string log;
    private List<FileInfo> videoFiles = new List<FileInfo>();
    private SynchronizedCollection<VideoEnty> videoCollection = new SynchronizedCollection<VideoEnty>();
    private Dictionary<string, VideoEnty> dicVideos = new();
    private ObservableCollection<string> paths = new ObservableCollection<string>();


    #endregion

    #region Properties

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

    public ObservableCollection<VideoEnty> TmpVideos
    {
        get => this._tmpVideos;
        set => this.SetProperty(ref this._tmpVideos, value);
    }

    public ObservableCollection<string> Paths
    {
        get => this.paths;
        set => this.SetProperty(ref this.paths, value);
    }

    public string Log
    {
        get => this.log;
        set => this.SetProperty(ref this.log, value);
    }

    #endregion

    #region Command

    public IAsyncRelayCommand ProcessDirCommand { get; set; }
    public IAsyncRelayCommand DeleteAllCommand { get; set; }
    public IAsyncRelayCommand LoadDirCommand { get; set; }
    public IAsyncRelayCommand SaveCommand { get; set; }

    #endregion

    #region API

    [RelayCommand]
    public async Task ProcessForDirAsync()
    {
        this.videoFiles?.Clear();
        this.dicVideos = this.Videos?.ToDictionary(mm => mm.VideoPath);
        var uri = this.DirPath;
        var dirInfo = new DirectoryInfo(uri);
        await this.ProcessForDirsAsync(dirInfo);
        await this.ProcessVideosAsync(dirInfo.Name);
        this.Videos = new ObservableCollection<VideoEnty>(this.videoCollection);
    }

    public async Task DeleteAllAsync()
    {
        var uri = this.DirPath;
        var dirInfo = new DirectoryInfo(uri);
        await Task.Run(() => DeleteAll(dirInfo));
    }

    public async Task LoadDirAsync()
    {
        this.Videos.Clear();
        var uri = this.DirPath;
        var dirInfo = new DirectoryInfo(uri);
        var _videos = await this.LoadDirAsync(dirInfo);

        if (_videos?.Any() ?? false)
        {
            this.Videos = new ObservableCollection<VideoEnty>(_videos.OrderByDescending(m => m.MidifyTime));
            var _tmpVideos = this.Videos.Take(20);
            this.TmpVideos = new ObservableCollection<VideoEnty>(_tmpVideos);
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var uri = this.DirPath;
            var dirInfo = new DirectoryInfo(uri);
            this.Save(dirInfo);

            MessageBox.Show("Save success!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void Play(object param)
    {
        try
        {
            string path = param as string;
            if (!string.IsNullOrWhiteSpace(path))
            {
                Process.Start(@"C:\Program Files\DAUM\PotPlayer\PotPlayerMini64.exe", path);

                var video = this.Videos.FirstOrDefault(m => m.VideoPath == path);
                video.PlayCount++;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        try
        {
            if (param is VideoEnty video)
            {
                File.Delete(video.VideoPath);

                this.Videos.Remove(video);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task ProcessVideoAsync(object param)
    {
        try
        {
            if (param is VideoEnty enty)
            {
                await this.ProcessVideoAsync(enty);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void ScrollChanged(dynamic parameter)
    { 
        if (parameter.VerticalOffset == parameter.ScrollableHeight)
        { 
            var index = this.TmpVideos.Count;
            var videoss = this.Videos.Skip(index).Take(2).ToList();

            if (videoss?.Any() ?? false)
            {
                foreach (var item in videoss)
                {
                    this.TmpVideos.Add(item);
                }
            }
        }
    }

    #endregion

    #region private

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
            else
            {
                if (this.videoCollection?.Any() ?? false)
                {
                    videoEnties = this.videoCollection.ToList();
                }
            }
        }
        catch (Exception ex)
        {
            this.Output($"Error: {dirInfo.FullName}{Environment.NewLine}{ex}");
            MessageBox.Show($"{dirInfo.FullName}{Environment.NewLine}{ex}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
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
        var picCount = 10;
        using var libVLC = new LibVLC();
        using var mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(libVLC);
        var jsonpath = Path.Combine(AppContext.BaseDirectory, "data", dirName);

        foreach (var item in fileInfos)
        {
            try
            {
                if (dicVideos?.ContainsKey(item.FullName) ?? false)
                    continue;

                var times = new List<long>(); // 截图时间点
                var images = new List<string>(); // 截图文件
                var length = await this.ParseMediaAsync(libVLC, item);
                var media = new Media(libVLC, item.FullName, FromType.FromPath); // 视频文件
                var interval = length / picCount; // 截图时间间隔
                mediaPlayer.Media = media; // 设置视频文件
                mediaPlayer.EncounteredError += (s, e) => { this.Output($"Error: {e}"); };

                for (int i = 0; i < picCount; i++)
                    times.Add(interval * i); // 添加播放时间  

                mediaPlayer.Play();
                mediaPlayer.ToggleMute(); // 静音

                while (mediaPlayer.State != VLCState.Playing)
                    Thread.Sleep(500);

                var videoEnty = new VideoEnty(); // 视频实体
                videoEnty.Caption = Path.GetFileNameWithoutExtension(item.Name); // 视频标题
                videoEnty.Length = item.Length / 1024 / 1024; // 视频大小
                videoEnty.VideoPath = item.FullName; // 视频路径
                videoEnty.MidifyTime = item.LastWriteTime; // 修改时间
                videoEnty.VideoDir = jsonpath;

                foreach (var time in times)
                {
                    await Task.Delay(500); // 等待截图完成

                    var picName = $"{Guid.NewGuid()}.png";
                    var snapshot = Path.Combine(jsonpath, picName);
                    images.Add(snapshot);

                    mediaPlayer.Time = time; // 设置播放时间
                    await Task.Delay(500); // 等待截图完成
                    mediaPlayer.TakeSnapshot(0, snapshot, 0, 0); // 截图
                }

                videoEnty.Snapshots = new ObservableCollection<string>(images);
                this.videoCollection.Add(videoEnty);
            }
            catch (Exception ex)
            {
                this.Output($"Error: {item.FullName}{Environment.NewLine}{ex}");
            }
            finally
            {
                mediaPlayer.Stop();
            }
        }

        mediaPlayer.Dispose();
    }

    private async Task ProcessVideoAsync(VideoEnty enty)
    {
        var picCount = 10;
        using var libVLC = new LibVLC();
        using var mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(libVLC);

        try
        {
            var item = new FileInfo(enty.VideoPath); // 视频文件
            var times = new List<long>(); // 截图时间点
            var images = new List<string>(); // 截图文件
            var length = await this.ParseMediaAsync(libVLC, item);
            var media = new Media(libVLC, item.FullName, FromType.FromPath); // 视频文件
            var interval = length / picCount; // 截图时间间隔
            var jsonpath = enty.VideoDir;
            mediaPlayer.Media = media; // 设置视频文件
            mediaPlayer.EncounteredError += (s, e) => { this.Output($"Error: {e}"); };

            for (int i = 0; i < picCount; i++)
                times.Add(interval * i); // 添加播放时间  

            mediaPlayer.Play();
            mediaPlayer.ToggleMute(); // 静音

            while (mediaPlayer.State != VLCState.Playing)
                Thread.Sleep(500);

            enty.Caption = Path.GetFileNameWithoutExtension(enty.VideoPath); // 视频标题
            enty.Length = enty.Length / 1024 / 1024; // 视频大小
            enty.VideoPath = item.FullName; // 视频路径
            enty.MidifyTime = item.LastWriteTime; // 修改时间

            foreach (var time in times)
            {
                await Task.Delay(200); // 等待截图完成

                var picName = $"{Guid.NewGuid()}.png";
                var snapshot = Path.Combine(jsonpath, picName);
                images.Add(snapshot);

                mediaPlayer.Time = time; // 设置播放时间
                await Task.Delay(100); // 等待截图完成
                mediaPlayer.TakeSnapshot(0, snapshot, 0, 0); // 截图
            }

            this.DeleteVideoImages(enty);
            enty.Snapshots = new ObservableCollection<string>(images);
        }
        catch (Exception ex)
        {
            this.Output($"Error: {enty.VideoPath}{Environment.NewLine}{ex}");
        }
        finally
        {
            mediaPlayer.Stop();
            mediaPlayer.Dispose();
        }
    }

    private void DeleteVideoImages(VideoEnty enty)
    {
        var imgs = enty.Snapshots.ToList();
        enty.Snapshots.Clear();
        foreach (var item in imgs)
        {
            try
            {
                File.Delete(item);
            }
            catch (Exception ex)
            {
                this.Output($"File Del Error:{item}{Environment.NewLine}{ex}");
            }
        }
    }

    /// <summary>
    /// 解析视频时长
    /// </summary> 
    private async Task<long> ParseMediaAsync(LibVLC libVLC, FileInfo item)
    {
        var media = new Media(libVLC, item.FullName, FromType.FromPath);
        await media.Parse(MediaParseOptions.ParseNetwork);
        media.ParseStop();
        var length = media.Duration;
        return length;
    }

    private void Save(DirectoryInfo dirInfo)
    {
        var jsonpath = Path.Combine(AppContext.BaseDirectory, "data", dirInfo.Name);

        if (!Directory.Exists(jsonpath))
            Directory.CreateDirectory(jsonpath);

        var json = string.Empty;

        if (this.Videos?.Any() ?? false)
            json = JsonConvert.SerializeObject(this.Videos, Formatting.Indented);
        else if (this.videoCollection?.Any() ?? false)
            json = JsonConvert.SerializeObject(this.videoCollection, Formatting.Indented);

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

    #endregion
}

public class VideoEnty : ObservableObject
{
    private string _caption;
    private string videoPath;
    private long length;
    private long playCount = 0;
    public DateTime? midifyTime;
    private ObservableCollection<string> snapshots;
    private string videoDir;

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

    public string VideoDir
    {
        get => this.videoDir;
        set => this.SetProperty(ref this.videoDir, value);
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

    public DateTime? MidifyTime
    {
        get => this.midifyTime;
        set => this.SetProperty(ref this.midifyTime, value);
    }

    public ObservableCollection<string> Snapshots
    {
        get => this.snapshots;
        set => this.SetProperty(ref this.snapshots, value);
    }
}
using System;
using System.Collections.Concurrent;
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
using Serilog;

namespace YExplorer;

public partial class MainViewModel : ObservableObject
{
    public MainViewModel()
    {
        //this.ProcessDirCommand = new AsyncRelayCommand(ProcessForDirAsync);
        this.DeleteAllCommand = new AsyncRelayCommand(DeleteAllAsync);
        this.LoadDirCommand = new AsyncRelayCommand(LoadDirAsync);
        this.SaveCommand = new AsyncRelayCommand(SaveAsync);

        var dirs = Directory.GetDirectories(@"\\192.168.10.2\99_资源收藏\01_成人资源");
        var dirs1 = Directory.GetDirectories(@"\\192.168.10.2\98_资源收藏\01_成人资源");
        this.paths = new ObservableCollection<string>(dirs.Concat(dirs1));
    }

    #region Fields

    private static List<string> picExt = new List<string> { ".jpg", ".png", ".gif", ".bmp" };

    private static List<string> videoExt = new List<string>
        { ".mp4", ".avi", ".mkv", ".rmvb", ".wmv", ".ts", ".m4v", ".mov", ".flv" };

    private static List<string> storeExt = new List<string> { ".aria2", ".torrent" };

    private decimal oneMbSize = 2 * 1024 * 1024;
    private decimal videoMaxMbSize = 100 * 1024 * 1024;
    private string dirPath;
    private string log;
    private List<FileInfo> videoFiles = new();
    private ObservableCollection<VideoEnty> videos = new();
    private ObservableCollection<VideoEnty> _tmpVideos = new();
    private SynchronizedCollection<VideoEnty> videoCollection = new();
    private ConcurrentDictionary<string, VideoEnty> dicVideos = new();
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
        try
        {
            this.videoFiles?.Clear();
            var tmpDics = this.Videos?.ToDictionary(mm => mm.VideoPath) ?? new();
            var uri = this.DirPath;
            var dirInfo = new DirectoryInfo(uri);
            this.dicVideos = new ConcurrentDictionary<string, VideoEnty>(tmpDics);
            this.videoCollection = new SynchronizedCollection<VideoEnty>(this.Videos);
            await this.ProcessForDirsAsync(dirInfo);

            Log.Information($"Scan videos count {this.videoFiles.Count}");

            for (int i = 0; i < this.videoFiles.Count; i++)
            {
                var vfile = this.videoFiles[i];
                if (this.dicVideos.ContainsKey(vfile.FullName))
                {
                    Log.Information($"{vfile.Name} Video already exists, processed.");
                    this.videoFiles.Remove(vfile);
                }
            }

            Log.Information($"Filterd videos count {this.videoFiles.Count}");

            await this.ProcessVideosAsync();
            this.Videos = new ObservableCollection<VideoEnty>(this.videoCollection);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task DeleteAllAsync()
    {
        try
        {
            var uri = this.DirPath;
            var dirInfo = new DirectoryInfo(uri);
            await Task.Run(() => DeleteAll(dirInfo));
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task LoadDirAsync()
    {
        try
        {
            this.Videos.Clear();
            this.TmpVideos.Clear();
            this.videoCollection.Clear();
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
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
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
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void Folder(object param)
    {
        try
        {
            string path = param as string;
            var dirPath = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(path))
            {
                Process.Start("explorer.exe", dirPath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
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
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void ScrollChanged(dynamic parameter)
    {
        try
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
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }


    }

    [RelayCommand]
    public void OpenLogs()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Process.Start("explorer.exe", path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region private

    private void DeleteAll(DirectoryInfo dirInfo)
    {
        Log.Information($"Start del {this.DirPath} ...");
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
            Log.Information($"Del {dirInfo.Name} images .");
            foreach (var item in picDelFiles)
                item.Delete();
        }

        if (videoDelFiles?.Any() ?? false)
        {
            Log.Information($"Del {dirInfo.Name} small videos .");
            foreach (var item in videoDelFiles)
                item.Delete();
        }

        if (otherDelFiles?.Any() ?? false)
        {
            Log.Information($"Del {dirInfo.Name} other files .");
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

        Log.Information($"End del {this.DirPath} .");
    }

    private async Task<List<VideoEnty>> LoadDirAsync(DirectoryInfo dirInfo)
    {
        Log.Information($"Start Load {dirInfo.Name} ...");
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
            Log.Information($"Error: {dirInfo.FullName}{Environment.NewLine}{ex}");
            MessageBox.Show($"{dirInfo.FullName}{Environment.NewLine}{ex}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Log.Information($"End Load {dirInfo.Name} ...");
        }

        return videoEnties;
    }

    private async Task ProcessForDirsAsync(DirectoryInfo dirInfo)
    {
        Log.Information($"Start Process {dirInfo.Name} ...");

        var files = dirInfo.GetFiles();
        var videoFiles = files.Where(f => videoExt.Contains(f.Extension.ToLower())).ToList();
        var videoStoreFiles = videoFiles.Where(m => m.Length >= videoMaxMbSize).ToList();
        if (videoStoreFiles?.Any() ?? false)
        {
            Log.Information($"Global video files added .");

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

        Log.Information($"End Process {dirInfo.Name} .");
    }

    private async Task ProcessVideosAsync(string dirName)
    {
        Log.Information($"Start Process Videos {dirName} .");

        var taskCount = 1;
        var batchSize = this.videoFiles.Count / taskCount;
        batchSize = batchSize <= 0 ? 1 : batchSize;

        var array = this.videoFiles.Chunk(batchSize).ToList();
        var jsonpath = Path.Combine(AppContext.BaseDirectory, "data", dirName);
        var tasks = new List<Task>(taskCount);

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

        Log.Information($"End Process Videos {dirName} .");
    }

    private async Task ProcessVideosAsync(string dirName, FileInfo[] fileInfos)
    {
        Log.Information($"Start Process Videos {dirName} , Video count :{fileInfos?.Length}.");

        var picCount = 10;
        using var libVLC = new LibVLC();
        using var mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(libVLC);
        var jsonpath = Path.Combine(AppContext.BaseDirectory, "data", dirName);

        foreach (var item in fileInfos)
        {
            try
            {
                if (dicVideos?.ContainsKey(item.FullName) ?? false)
                {
                    Log.Information($"{item.Name} Video already exists, processed.");
                    continue;
                }

                Log.Information($"Process Video {item.Name}.");
                var times = new List<long>(); // 截图时间点
                var images = new List<string>(); // 截图文件
                var length = await this.ParseMediaAsync(libVLC, item);
                var media = new Media(libVLC, item.FullName, FromType.FromPath); // 视频文件
                var interval = length / picCount; // 截图时间间隔
                mediaPlayer.Media = media; // 设置视频文件
                mediaPlayer.EncounteredError += (s, e) => { Log.Information($"Error: {e}"); };

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

                this.dicVideos[videoEnty.VideoPath] = videoEnty;
                var json = JsonConvert.SerializeObject((this.Videos?.Any() ?? false) ? this.Videos : this.videoCollection);
                File.WriteAllTextAsync(Path.Combine(jsonpath, $"{dirName}.json"), json);
            }
            catch (Exception ex)
            {
                Log.Information($"Error: {item.FullName}{Environment.NewLine}{ex}");
            }
            finally
            {
                mediaPlayer.Stop();
            }
        }

        mediaPlayer.Dispose();
        Log.Information($"End Process Videos {dirName} , Video count :{fileInfos?.Length}.");
    }

    private async Task ProcessVideosAsync()
    {
        Log.Information($"Start Process Videos , Video count :{this.videoFiles.Count}.");

        var picCount = 10;
        using LibVLC? libVLC = new LibVLC();
        using var mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(libVLC);
        var jsonpath = Path.Combine(AppContext.BaseDirectory, "data", this.dirPath);
        var jsonfile = $"{Path.GetDirectoryName(this.dirPath)}.json";

        foreach (var item in this.videoFiles)
        {
            try
            {
                if (dicVideos?.ContainsKey(item.FullName) ?? false)
                {
                    Log.Information($"{item.Name} Video already exists, processed.");
                    continue;
                }

                Log.Information($"Process Video {item.Name}.");
                var times = new List<long>(); // 截图时间点
                var images = new List<string>(); // 截图文件
                var length = await this.ParseMediaAsync(libVLC, item);
                var media = new Media(libVLC, item.FullName, FromType.FromPath); // 视频文件
                var interval = length / picCount; // 截图时间间隔
                mediaPlayer.Media = media; // 设置视频文件
                mediaPlayer.EncounteredError += (s, e) => { Log.Information($"Error: {e}"); };

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
                    var picName = $"{Guid.NewGuid()}.png";
                    var snapshot = Path.Combine(jsonpath, picName);
                    images.Add(snapshot);

                    mediaPlayer.Time = time; // 设置播放时间
                    await Task.Delay(100); // 等待截图完成
                    mediaPlayer.TakeSnapshot(0, snapshot, 0, 0); // 截图
                    await Task.Delay(100); // 等待截图完成
                }

                videoEnty.Snapshots = new ObservableCollection<string>(images);
                this.videoCollection.Add(videoEnty);

                this.dicVideos[videoEnty.VideoPath] = videoEnty;
                var json = JsonConvert.SerializeObject((this.Videos?.Any() ?? false) ? this.Videos : this.videoCollection);
                File.WriteAllTextAsync(Path.Combine(jsonpath, jsonfile), json);
            }
            catch (Exception ex)
            {
                Log.Information($"Error: {item.FullName}{Environment.NewLine}{ex}");
            }
            finally
            {
                mediaPlayer.Stop();
            }
        }

        mediaPlayer.Dispose();
        Log.Information($"End Process Videos , Video count :{this.videoFiles.Count}.");
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
            mediaPlayer.EncounteredError += (s, e) => { Log.Information($"Error: {e}"); };

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
            Log.Information($"Error: {enty.VideoPath}{Environment.NewLine}{ex}");
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
                Log.Information($"File Del Error:{item}{Environment.NewLine}{ex}");
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
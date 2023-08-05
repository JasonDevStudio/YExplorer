﻿using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using Newtonsoft.Json;
using Serilog;
using UraniumUI.Dialogs.CommunityToolkit;
using YMauiExplorer.Models;

namespace YMauiExplorer.ViewModels;

/// <summary>
/// MainViewModel 类，它继承自 ObservableObject 类。
/// </summary>
/// <remarks>
/// 这个类是 ViewModel 部分，它处理视图中的业务逻辑，并通过数据绑定将数据从 Model 传递到 View。
/// 在这个类中，可能会定义一些属性和命令，这些属性和命令绑定到视图的控件，以实现界面的各种功能。
/// </remarks>
public partial class MainViewModel : ObservableObject
{
    /// <summary>
    /// 初始化MainViewModel类的新实例。
    /// </summary>
    public MainViewModel()
    {
        var dirs = Directory.GetDirectories(@"\\192.168.10.2\99_资源收藏\01_成人资源");
        var dirs1 = Directory.GetDirectories(@"\\192.168.10.2\98_资源收藏\01_成人资源");
        this.DirPaths = new ObservableCollection<string>(dirs.Concat(dirs1));
        this.dataPath = AppSettingsUtils.Default.WinDataPath;
        this.playerPath = AppSettingsUtils.Default.WinPlayerPath;

#if MACCATALYST
        this.dataPath = AppSettingsUtils.Default.MacDataPath;
        this.playerPath = AppSettingsUtils.Default.MacPlayerPath;
#endif
    }

    #region Static

    /// <summary>
    /// 支持的图片扩展名列表
    /// </summary>
    private static readonly List<string> picExt = new List<string> { ".jpg", ".png", ".gif", ".bmp" };

    /// <summary>
    /// 支持的视频扩展名列表
    /// </summary>
    private static readonly List<string> videoExt = new List<string>
    { ".mp4", ".avi", ".mkv", ".rmvb", ".wmv", ".ts", ".m4v", ".mov", ".flv" };

    /// <summary>
    /// 存储文件的扩展名列表
    /// </summary>
    private static readonly List<string> storeExt = new List<string> { ".aria2", ".torrent" };

    /// <summary>
    /// 表示1MB的大小（单位：字节）
    /// </summary>
    private readonly decimal oneMbSize = 2 * 1024 * 1024;

    /// <summary>
    /// 视频最大MB大小（单位：字节）
    /// </summary>
    private readonly decimal videoMaxMbSize = 100 * 1024 * 1024;

    #endregion

    #region Fields

    /// <summary>
    /// 数据存储目录
    /// </summary>
    private string dataPath;

    /// <summary>
    /// 播放器路径
    /// </summary>
    private string playerPath;

    /// <summary>
    /// 视频条目迭代器
    /// </summary>
    private IEnumerator dataEnumerator;

    /// <summary>
    /// 全量视频条目列表
    /// </summary>
    private List<VideoEntry> allVideos = new();

    /// <summary>
    /// 用于同步的锁对象
    /// </summary>
    private object lockObj = new();

    /// <summary>
    /// 加载数量 默认1
    /// </summary>
    private int loadCount = 1;

    /// <summary>
    /// 以字符串为键，VideoEntry为值的线程安全字典
    /// </summary>
    private ConcurrentDictionary<string, VideoEntry> dicVideos = new();
    #endregion

    #region Property

    /// <summary>
    /// 选中目录
    /// </summary>
    [ObservableProperty]
    private string selectedDir;

    /// <summary>
    /// 文件目录集合
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> dirPaths = new();

    /// <summary>
    /// 视频条目列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<VideoEntry> videos = new();

    #endregion

    #region ICommand

    public RelayCommand<object> PlayCommand => new RelayCommand<object>(this.Play);

    #endregion

    #region API

    /// <summary>
    /// 异步加载指定目录下的视频实体。
    /// </summary>
    /// <returns>
    /// 表示异步操作的任务。
    /// </returns>
    /// <remarks>
    /// 此方法首先清空当前的视频列表和临时视频列表，然后获取存储数据的目录路径，并加载该目录下的视频实体。如果加载的视频实体列表不为空，那么它将这些视频实体按照修改时间的降序排列并设置为当前的视频列表。然后，它取出前5个视频实体，设置为临时视频列表。
    /// </remarks>
    [RelayCommand]
    public async Task LoadDirAsync()
    {
        try
        {
            this.Videos.Clear();
            this.allVideos.Clear();
            this.loadCount = 1;
            var dirInfo = new DirectoryInfo(this.SelectedDir);
            this.allVideos = await this.LoadDirAsync(dirInfo);

            if (this.allVideos?.Any() ?? false)
                this.LoadNextItem(10);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            CommunityToolkitDialogExtensions.ConfirmAsync(Application.Current.MainPage, "Error", $"{ex}");
        }
    }

    /// <summary>
    /// 异步加载所有目录下的视频实体。
    /// </summary>
    /// <returns>
    /// 表示异步操作的任务。
    /// </returns>
    /// <remarks>
    /// 此方法首先清空当前的视频列表和临时视频列表，然后获取存储数据的目录路径，并加载该目录下的视频实体。如果加载的视频实体列表不为空，那么它将这些视频实体按照修改时间的降序排列并设置为当前的视频列表和临时视频列表。
    /// </remarks>
    [RelayCommand]
    public async Task LoadAllDirsAsync()
    {
        try
        {
            this.Videos.Clear();
            this.allVideos.Clear();
            this.loadCount = 50;
            var dirInfo = new DirectoryInfo(this.SelectedDir);
            this.allVideos = await this.LoadDirAsync(dirInfo);

            if (this.allVideos?.Any() ?? false)
                this.LoadNextItem(this.loadCount);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            CommunityToolkitDialogExtensions.ConfirmAsync(Application.Current.MainPage, "Error", $"{ex}");
        }
    }

    /// <summary>
    /// 异步处理指定目录下的视频。
    /// </summary>
    /// <returns>
    /// 表示异步操作的任务。
    /// </returns>
    /// <remarks>
    /// 此方法首先清空当前的视频文件列表，然后将当前的视频实体列表转换为字典。之后，获取存储数据的目录路径，创建一个并发字典来存储视频实体，将视频实体列表转换为同步集合。然后，调用`ProcessForDirsAsync`方法处理指定目录下的所有视频，并清除已存在的视频。最后，将处理后的视频集合设置为当前的视频实体列表。
    /// </remarks>
    [RelayCommand]
    public async Task ProcessVideosAsync()

    {
        try
        {
            var videoFiles = new List<FileInfo>();
            var tmpDics = this.allVideos?.ToDictionary(mm => mm.VideoPath) ?? new();
            var dirInfo = new DirectoryInfo(this.SelectedDir);
            this.dicVideos = new ConcurrentDictionary<string, VideoEntry>(tmpDics);

            var files = await this.ProcessForDirsAsync(dirInfo);

            Log.Information($"Scan videos count {files.Count}");

            files = this.ClearExists(files);

            Log.Information($"Filterd videos count {files.Count}");

            await this.ProcessVideosAsync(files);
            
            this.LoadNextItem(this.loadCount);

            Log.Information($"Process videos End。");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            CommunityToolkitDialogExtensions.ConfirmAsync(Application.Current.MainPage, "Error", $"{ex}");
        }
    }

    /// <summary>
    /// 播放指定路径的视频文件。
    /// </summary>
    /// <param name="param">表示文件路径的对象。</param>
    /// <remarks>
    /// 此方法首先将传入的参数转换为字符串路径，然后检查路径是否为空。如果路径不为空，那么它会使用PotPlayer播放器打开并播放该路径的视频文件，然后增加该视频的播放次数。
    /// </remarks> 
    public void Play(object param)
    {
        try
        {
            string path = param as string;
            if (!string.IsNullOrWhiteSpace(path))
            {
                Process.Start(this.playerPath, path);

                var video = this.Videos.FirstOrDefault(m => m.VideoPath == path);
                video.PlayCount++;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            CommunityToolkitDialogExtensions.ConfirmAsync(Application.Current.MainPage, "Error", $"{ex}");
        }
    }

    /// <summary>
    /// 处理滚动事件。
    /// </summary>
    /// <param name="parameter">包含滚动参数的动态对象。</param>
    /// <remarks>
    /// 当滚动条滚动到底部时，此方法会从原始的视频列表中获取更多的视频，并添加到临时视频列表中。
    /// </remarks>
    [RelayCommand]
    public void ScrollChanged(dynamic parameter)
    {
        try
        {
            LoadNextItem(this.loadCount);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            CommunityToolkitDialogExtensions.ConfirmAsync(Application.Current.MainPage, "Error", $"{ex}");
        }
    }
    #endregion

    #region Private

    /// <summary>
    /// 异步加载指定目录下的视频实体列表。
    /// </summary>
    /// <param name="dirInfo">表示目标目录的对象。</param>
    /// <returns>
    /// 表示异步操作的任务，任务的结果是视频实体列表。
    /// </returns>
    /// <remarks>
    /// 此方法首先获取数据文件的路径，然后检查该文件是否存在。如果文件存在，它会从文件中读取JSON字符串，并将其反序列化为视频实体列表。
    /// 如果文件不存在，它会检查是否有可用的视频实体集合，并将其转换为列表。
    /// </remarks>
    private async Task<List<VideoEntry>> LoadDirAsync(DirectoryInfo dirInfo)
    {
        Log.Information($"Start Load {dirInfo.Name} ...");

        try
        {
            var dataConf = this.GetDataDirPath();
            var dataDirPath = dataConf.dir;
            var jsonfile = dataConf.file;
            if (File.Exists(jsonfile))
            {
                var json = await File.ReadAllTextAsync(jsonfile);
                this.allVideos = JsonConvert.DeserializeObject<List<VideoEntry>>(json);
                this.allVideos = this.allVideos.OrderByDescending(x => x.MidifyTime).ToList();
                this.dataEnumerator = this.allVideos.GetEnumerator();
            }

            foreach (var item in this.allVideos)
            {
                item.Dir = Path.GetDirectoryName(item.VideoPath);
                item.Dir = item.Dir.Replace(this.SelectedDir, string.Empty).Trim('\\');
            }
        }
        catch (Exception ex)
        {
            Log.Information($"Error: {dirInfo.FullName}{Environment.NewLine}{ex}");

            CommunityToolkitDialogExtensions.ConfirmAsync(Application.Current.MainPage, "Error", $"{dirInfo.FullName}{Environment.NewLine}{ex}");
        }
        finally
        {
            Log.Information($"End Load {dirInfo.Name} ...");
        }

        return this.allVideos;
    }

    /// <summary>
    /// 获取数据目录路径
    /// </summary>
    /// <returns>包含目录路径、文件路径和名称的元组</returns>
    private (string dir, string file, string name) GetDataDirPath()
    {
        var dirInfo = new DirectoryInfo(this.SelectedDir);
        var dataDirPath = Path.Combine(this.dataPath, dirInfo.Name);
        var name = $"data.json";
        var jsonfile = Path.Combine(dataDirPath, name);
        return (dataDirPath, jsonfile, name);
    }

    /// <summary>
    /// 从全量数据集合中加载下N项。
    /// </summary>
    /// <param name="count">需要加载的数量</param>
    private void LoadNextItem(int count = 1)
    {
        if (count == -1)
        {
            count = this.allVideos.Count;
            this.Videos = new ObservableCollection<VideoEntry>(this.allVideos);
        }
        else
        {
            if (this.dataEnumerator == null)
                return;

            for (int i = 0; i < count; i++)
            {
                if (this.dataEnumerator.MoveNext())
                    this.Videos.Add((VideoEntry)this.dataEnumerator.Current);
            }
        }
    }

    /// <summary>
    /// 异步处理指定目录及其所有子目录下的视频文件。
    /// </summary>
    /// <param name="dirInfo">表示目标目录的对象。</param>
    /// <returns>
    /// 表示异步操作的任务。
    /// </returns>
    /// <remarks>
    /// 此方法首先获取指定目录下的所有文件，并筛选出视频文件。然后，它将文件大小大于设定阈值的视频文件添加到全局视频文件列表中。
    /// 接着，这个方法获取指定目录下的所有子目录，并递归地对每一个子目录执行同样的操作。
    /// </remarks>
    private async Task<List<FileInfo>> ProcessForDirsAsync(DirectoryInfo dirInfo)
    {
        Log.Debug($"Start Process {dirInfo.Name} ...");

        var files = dirInfo.GetFiles();
        var videoFiles = files.Where(f => videoExt.Contains(f.Extension.ToLower())).ToList();
        var videoStoreFiles = videoFiles.Where(m => m.Length >= videoMaxMbSize).ToList() ?? new();

        var chidDirs = dirInfo.GetDirectories();
        if (chidDirs?.Any() ?? false)
        {
            foreach (var item in chidDirs)
            {
                var chidFiles = await ProcessForDirsAsync(item);
                if (chidFiles?.Any() ?? false)
                    videoStoreFiles.AddRange(chidFiles);
            }
        }

        Log.Debug($"End Process {dirInfo.Name} .");

        return videoStoreFiles;
    }

    /// <summary>
    /// 清除已存在的视频文件。
    /// </summary> 
    private List<FileInfo> ClearExists(List<FileInfo> files)
    { 
        for (int i = files.Count - 1; i >= 0; i--)
        {
            var vfile = files[i];
            if (this.dicVideos.ContainsKey(vfile.FullName))
            {
                Log.Debug($"{vfile.Name} Video already exists, processed.");
                files.Remove(vfile);
            }
        }

        return files;
    }


    /// <summary>
    /// 异步处理所有视频文件。
    /// </summary>
    /// <param name="files">需要解析处理的视频文件集合</param>
    /// <returns>
    /// 表示异步操作的任务。
    /// </returns>
    /// <remarks>
    /// 此方法首先将所有的视频文件分割成等大小的批次，然后为每个批次创建一个新的任务来处理。
    /// 所有的任务都是并行运行的，以提高处理速度。当所有的任务都完成后，这个方法就结束。
    /// </remarks>
    private async Task ProcessVideosAsync(List<FileInfo> files, int taskCount = 1)
    {
        Log.Information($"Start Process Videos ...");
         
        var batchSize = files.Count / taskCount;
        batchSize = batchSize <= 0 ? 1 : batchSize;

        var array = files.Chunk(batchSize).ToList();
        var dataConf = this.GetDataDirPath();
        var dataDirPath = dataConf.dir;
        var jsonfile = dataConf.file;
        var tasks = new List<Task>(taskCount);

        if (!Directory.Exists(dataDirPath))
            Directory.CreateDirectory(dataDirPath);

        for (int i = 0; i < array.Count; i++)
        {
            tasks.Add(Task.Factory.StartNew(async obj =>
            {
                var videos = obj as FileInfo[];
                await ProcessVideosAsync(videos);
            }, array[i]));
        }

        await Task.WhenAll(tasks);

        Log.Information($"End Process Videos .");
    }

    /// <summary>
    /// 异步处理一组视频文件。
    /// </summary>
    /// <param name="fileInfos">包含视频文件信息的数组。</param>
    /// <param name="picCount">包含截图图片数量的参数，默认10张。</param>
    /// <returns>
    /// 表示异步操作的任务。
    /// </returns>
    /// <remarks>
    /// 此方法使用LibVLC库初始化一个MediaPlayer对象，并打开和处理一组视频文件。然后在指定的时间间隔内抓取视频帧并将其保存为图像。
    /// 此外，它还创建一个包含视频的元数据和快照的实体，然后将这些实体序列化为JSON，并将其保存到文件中。
    /// </remarks>
    private async Task ProcessVideosAsync(FileInfo[] fileInfos, int picCount = 10)
    {
        Log.Information($"Start Process Videos , Video count :{fileInfos?.Length}.");

        using var libVLC = new LibVLC();
        using var mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(libVLC);
        var (datapath, jsonfile, name) = this.GetDataDirPath();

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
                using var media = new Media(libVLC, item.FullName, FromType.FromPath); // 视频文件
                var interval = length / picCount; // 截图时间间隔
                mediaPlayer.Media = media; // 设置视频文件
                mediaPlayer.EncounteredError += (s, e) => { Log.Information($"Error: {e}"); };

                for (int i = 0; i < picCount; i++)
                    times.Add(interval * i); // 添加播放时间  

                times.RemoveAt(0); // 移除第一个时间点
                times.RemoveAt(times.Count - 1); // 移除最后一个时间点
                mediaPlayer.Play();
                mediaPlayer.ToggleMute(); // 静音
                await Task.Delay(500);

                while (mediaPlayer.State != VLCState.Playing)
                {
                    await Task.Delay(500);

                    if (mediaPlayer.State == VLCState.Ended ||
                        mediaPlayer.State == VLCState.Error ||
                        mediaPlayer.State == VLCState.Stopped ||
                        mediaPlayer.State == VLCState.NothingSpecial)
                    {
                        Log.Error($"Error: {mediaPlayer.State}");
                        break;
                    }
                }

                if (mediaPlayer.State != VLCState.Playing)
                    continue;

                var VideoEntry = new VideoEntry(); // 视频实体
                VideoEntry.Caption = Path.GetFileNameWithoutExtension(item.Name); // 视频标题
                VideoEntry.Length = item.Length / 1024 / 1024; // 视频大小
                VideoEntry.VideoPath = item.FullName; // 视频路径
                VideoEntry.MidifyTime = item.LastWriteTime; // 修改时间
                VideoEntry.VideoDir = datapath;

                foreach (var time in times)
                {
                    var picName = $"{Guid.NewGuid()}.png";
                    var snapshot = Path.Combine(datapath, picName);
                    images.Add(snapshot);

                    mediaPlayer.Time = time; // 设置播放时间
                    await Task.Delay(500); // 等待截图完成
                    mediaPlayer.TakeSnapshot(0, snapshot, 0, 0); // 截图
                }

                VideoEntry.Snapshots = new ObservableCollection<string>(images);
                this.allVideos.Add(VideoEntry);
                this.dicVideos[VideoEntry.VideoPath] = VideoEntry;
                var json = JsonConvert.SerializeObject(this.allVideos);

                lock (lockObj)
                    File.WriteAllText(jsonfile, json);
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
        Log.Information($"End Process Videos , Video count :{fileInfos?.Length}.");
    }

    /// <summary>
    /// 异步解析视频文件并返回其长度。
    /// </summary>
    /// <param name="libVLC">LibVLC库的实例。</param>
    /// <param name="item">代表视频文件的对象。</param>
    /// <returns>
    /// 返回一个任务，该任务表示异步操作，任务的结果是视频文件的长度。
    /// </returns>
    /// <remarks>
    /// 此方法首先创建一个 'Media' 对象，并以异步方式解析这个媒体。解析完成后，它会停止解析并获取媒体的长度。
    /// </remarks> 
    private async Task<long> ParseMediaAsync(LibVLC libVLC, FileInfo item)
    {
        var media = new Media(libVLC, item.FullName, FromType.FromPath);
        await media.Parse(MediaParseOptions.ParseNetwork);
        media.ParseStop();
        var length = media.Duration;
        return length;
    }

    #endregion
}

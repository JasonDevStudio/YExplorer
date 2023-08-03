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
        this.paths = new ObservableCollection<string>(dirs.Concat(dirs1));
    }

    #region Fields

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

    /// <summary>
    /// 目录路径
    /// </summary>
    private string dirPath;

    /// <summary>
    /// 日志
    /// </summary>
    private string log;

    /// <summary>
    /// 视频文件列表
    /// </summary>
    private List<FileInfo> videoFiles = new();

    /// <summary>
    /// 视频条目列表
    /// </summary>
    private ObservableCollection<VideoEntry> videos = new();

    /// <summary>
    /// 临时视频条目列表
    /// </summary>
    private ObservableCollection<VideoEntry> _tmpVideos = new();

    /// <summary>
    /// 线程安全的视频条目集合
    /// </summary>
    private SynchronizedCollection<VideoEntry> videoCollection = new();

    /// <summary>
    /// 以字符串为键，VideoEntry为值的线程安全字典
    /// </summary>
    private ConcurrentDictionary<string, VideoEntry> dicVideos = new();

    /// <summary>
    /// 路径的集合
    /// </summary>
    private ObservableCollection<string> paths = new ObservableCollection<string>();

    /// <summary>
    /// 用于同步的锁对象
    /// </summary>
    private object lockObj = new();

    /// <summary>
    /// 主窗口
    /// </summary>
    private Window manWindow = Application.Current.MainWindow;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the directory path.
    /// </summary>
    public string DirPath
    {
        get => this.dirPath;
        set => this.SetProperty(ref this.dirPath, value);
    }

    /// <summary>
    /// Gets or sets the collection of videos.
    /// </summary>
    public ObservableCollection<VideoEntry> Videos
    {
        get => this.videos;
        set => this.SetProperty(ref this.videos, value);
    }

    /// <summary>
    /// Gets or sets the collection of temporary videos.
    /// </summary>
    public ObservableCollection<VideoEntry> TmpVideos
    {
        get => this._tmpVideos;
        set => this.SetProperty(ref this._tmpVideos, value);
    }

    /// <summary>
    /// Gets or sets the collection of paths.
    /// </summary>
    public ObservableCollection<string> Paths
    {
        get => this.paths;
        set => this.SetProperty(ref this.paths, value);
    }

    #endregion

    #region Command

    #endregion

    #region API

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
    public async Task ProcessForDirAsync()

    {
        try
        {
            this.videoFiles?.Clear();
            var tmpDics = this.Videos?.ToDictionary(mm => mm.VideoPath) ?? new();
            var uri = this.DirPath;
            var dirInfo = new DirectoryInfo(uri);
            this.dicVideos = new ConcurrentDictionary<string, VideoEntry>(tmpDics);
            this.VideosToSynchronizedCollection();
            await this.ProcessForDirsAsync(dirInfo);

            Log.Information($"Scan videos count {this.videoFiles.Count}");

            this.ClearExists();

            Log.Information($"Filterd videos count {this.videoFiles.Count}");

            await this.ProcessAllVideosAsync();
            this.Videos = new ObservableCollection<VideoEntry>(this.videoCollection);

            Log.Information($"Process videos End。");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show(manWindow, $"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 异步删除所有指定的数据。
    /// </summary>
    /// <returns>
    /// 表示异步操作的任务。
    /// </returns>
    /// <remarks>
    /// 此方法首先获取存储数据的目录路径，然后调用`DeleteAll`方法删除所有数据。
    /// </remarks>
    [RelayCommand]
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
            MessageBox.Show(manWindow, $"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

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
            this.TmpVideos.Clear();
            this.videoCollection.Clear();
            var uri = this.DirPath;
            var dirInfo = new DirectoryInfo(uri);
            var _videos = await this.LoadDirAsync(dirInfo);

            if (_videos?.Any() ?? false)
            {
                this.Videos = new ObservableCollection<VideoEntry>(_videos.OrderByDescending(m => m.MidifyTime));
                var _tmpVideos = this.Videos.Take(5);
                this.TmpVideos = new ObservableCollection<VideoEntry>(_tmpVideos);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show(manWindow, $"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            this.TmpVideos.Clear();
            this.videoCollection.Clear();
            var uri = this.DirPath;
            var dirInfo = new DirectoryInfo(uri);
            var _videos = await this.LoadDirAsync(dirInfo);

            if (_videos?.Any() ?? false)
            {
                this.Videos = new ObservableCollection<VideoEntry>(_videos.OrderByDescending(m => m.MidifyTime));

                this.TmpVideos = this.Videos;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show(manWindow, $"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 异步保存当前的数据状态。
    /// </summary>
    /// <returns>
    /// 表示异步操作的任务。
    /// </returns>
    /// <remarks>
    /// 此方法首先获取存储数据的目录路径，然后调用`Save`方法保存数据。最后，它返回一个已完成的任务，表示异步操作已完成。
    /// </remarks>
    [RelayCommand]
    public async Task SaveAsync()
    {
        try
        {
            var uri = this.DirPath;
            var dirInfo = new DirectoryInfo(uri);
            this.Save(dirInfo);

            //MessageBox.Show(manWindow,"Save success!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show(manWindow, $"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 播放指定路径的视频文件。
    /// </summary>
    /// <param name="param">表示文件路径的对象。</param>
    /// <remarks>
    /// 此方法首先将传入的参数转换为字符串路径，然后检查路径是否为空。如果路径不为空，那么它会使用PotPlayer播放器打开并播放该路径的视频文件，然后增加该视频的播放次数。
    /// </remarks>
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
            MessageBox.Show(manWindow, $"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 打开包含指定文件的文件夹。
    /// </summary>
    /// <param name="param">表示文件路径的对象。</param>
    /// <remarks>
    /// 此方法首先将传入的参数转换为字符串，然后获取该路径的目录名。最后，如果路径不为空，它会使用Windows资源管理器打开该目录。
    /// </remarks>
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
            MessageBox.Show(manWindow, $"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 删除一个视频实体及其对应的视频文件。
    /// </summary>
    /// <param name="param">表示视频实体的对象。</param>
    /// <remarks>
    /// 此方法首先检查传入的参数是否为`VideoEntry`类型，如果是，那么它就会删除视频文件，然后从视频列表中移除该视频实体，并保存更改。
    /// </remarks>
    [RelayCommand]
    public void Delete(object param)
    {
        try
        {
            if (param is VideoEntry video)
            {
                if (File.Exists(video.VideoPath))
                    File.Delete(video.VideoPath);

                this.Videos.Remove(video);
                this.TmpVideos.Remove(video);
                this.SaveAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show(manWindow, $"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 删除视频实体对应的文件夹及其内容。
    /// </summary>
    /// <param name="param">表示视频实体的对象。</param>
    /// <remarks>
    /// 此方法首先检查传入的参数是否为`VideoEntry`类型，如果是，它会弹出一个消息框询问用户是否确定要删除文件夹。
    /// 如果用户选择"否"，方法就会返回。如果用户选择"是"，那么它会从视频列表中找出所有在该文件夹内的视频实体，删除对应的视频文件，
    /// 然后从视频列表中移除这些实体。最后，它会删除整个文件夹，然后保存更改。
    /// </remarks>
    [RelayCommand]
    public void DeleteFolder(object param)
    {
        try
        {
            if (param is VideoEntry video)
            {
                MessageBoxResult result = MessageBox.Show(manWindow, $"Are you sure you want to delete the folder {video.VideoPath}?", "Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                    return;

                var dirName = Path.GetDirectoryName(video.VideoPath);

                var videos = this.Videos.Where(m => m.VideoPath.StartsWith(dirName)).ToList();
                foreach (var item in videos)
                {
                    if (File.Exists(item.VideoPath))
                        File.Delete(item.VideoPath);

                    this.Videos.Remove(item);
                    this.TmpVideos.Remove(item);
                }

                if (Directory.Exists(dirName))
                    Directory.Delete(dirName, true);

                this.SaveAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show(manWindow, $"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 异步处理单个视频文件。
    /// </summary>
    /// <param name="param">表示视频实体的对象。</param>
    /// <returns>
    /// 表示异步操作的任务。
    /// </returns>
    /// <remarks>
    /// 此方法首先检查传入的参数是否为`VideoEntry`类型，如果是，它就会调用`ProcessVideoAsync`方法来处理这个视频实体。
    /// </remarks>
    [RelayCommand]
    public async Task ProcessVideoAsync(object param)
    {
        try
        {
            if (param is VideoEntry enty)
            {
                await this.ProcessVideoAsync(enty);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show(manWindow, $"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show(manWindow, $"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }


    }

    /// <summary>
    /// 打开日志目录。
    /// </summary>
    /// <remarks>
    /// 此方法首先获取当前应用程序域的基目录，然后构造日志目录的路径。最后，它使用Windows资源管理器打开日志目录。
    /// </remarks>
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
            MessageBox.Show(manWindow, $"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 打开数据目录。
    /// </summary>
    /// <remarks>
    /// 此方法首先获取当前应用程序域的基目录，然后构造数据目录的路径。最后，它使用Windows资源管理器打开数据目录。
    /// </remarks>
    [RelayCommand]
    public void OpenDataDir()
    {
        try
        {
            var baseDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            var path = Path.Combine(baseDir.Parent.FullName, "data");
            Process.Start("explorer.exe", path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show(manWindow, $"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 清理指定目录下的部分数据。
    /// </summary>
    /// <remarks>
    /// 此方法首先获取数据目录的路径，并创建一个字典来存储所有的视频实体及其对应的快照文件。然后，它检查数据目录是否存在，
    /// 如果存在，它会获取目录中所有的PNG文件。接着，这个方法删除不在字典中的文件，也就是说，它会删除那些不是任何视频实体的快照的文件。
    /// </remarks>
    [RelayCommand]
    public void ClearDataDir()
    {
        try
        {
            var delCount = 0;
            var dirInfo = new DirectoryInfo(this.dirPath);
            var dataConf = this.GetDataDirPath();
            var dataDirPath = dataConf.dir;
            var dicFiles = new Dictionary<string, VideoEntry>();
            var dataDir = new DirectoryInfo(dataDirPath);

            if (this.Videos?.Any() ?? false)
            {
                foreach (var item in this.Videos)
                {
                    foreach (var path in item.Snapshots)
                    {
                        dicFiles.Add(path, item);
                    }
                }
            }

            if (dataDir.Exists)
            {
                var files = dataDir.GetFiles("*.png");
                var length = files.Length;

                if (files?.Any() ?? false)
                {
                    for (int i = length - 1; i >= 0; i--)
                    {
                        var file = files[i];

                        if (!dicFiles.ContainsKey(file.FullName))
                        {
                            try
                            {
                                file.Delete();
                                delCount++;
                            }
                            catch (Exception)
                            {
                                Log.Warning($"Del Error : {file.FullName}");
                            }
                        }
                    }
                }

            }

            MessageBox.Show(manWindow, $"清理数据资源完成, 清理文件 {delCount} 个。", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show(manWindow, $"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 清除指定目录下的所有数据。
    /// </summary>
    /// <remarks>
    /// 此方法首先获取数据目录的路径，然后检查该目录是否存在。如果存在，它会删除该目录及其所有子目录和文件。
    /// </remarks>
    [RelayCommand]
    public void ClearData()
    {
        try
        {
            var delCount = 0;
            var dirInfo = new DirectoryInfo(this.dirPath);
            var dataConf = this.GetDataDirPath();
            var dataDirPath = dataConf.dir;
            var dataDir = new DirectoryInfo(dataDirPath);

            if (dataDir.Exists)
            {
                dataDir.Delete(true);
            }

            MessageBox.Show(manWindow, $"清理数据资源完成", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show(manWindow, $"{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region private

    /// <summary>
    /// 删除指定目录及其所有子目录下的部分文件。
    /// </summary>
    /// <param name="dirInfo">表示目标目录的对象。</param>
    /// <remarks>
    /// 此方法首先获取指定目录下的所有文件，并根据文件的扩展名和大小将其分为不同的类别。然后，它会删除满足特定条件的文件，
    /// 包括图片文件、小于某个阈值的视频文件，以及除了图片、视频和特定文件之外的所有其他文件。最后，这个方法递归地对每一个子目录执行同样的操作，
    /// 如果一个目录中没有大于某个阈值的视频文件，那么这个目录会被删除。
    /// </remarks>
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
        var videoEnties = new List<VideoEntry>();

        try
        {
            var dataConf = this.GetDataDirPath();
            var dataDirPath = dataConf.dir;
            var jsonfile = dataConf.file;
            if (File.Exists(jsonfile))
            {
                var json = await File.ReadAllTextAsync(jsonfile);
                videoEnties = JsonConvert.DeserializeObject<List<VideoEntry>>(json); 
            }
            else
            {
                if (this.videoCollection?.Any() ?? false)
                {
                    videoEnties = this.videoCollection.ToList();
                }
            }

            foreach (var item in videoEnties)
            {
                item.Dir = Path.GetDirectoryName(item.VideoPath);
                item.Dir = item.Dir.Replace(this.DirPath, string.Empty).Trim('\\');
            }
        }
        catch (Exception ex)
        {
            Log.Information($"Error: {dirInfo.FullName}{Environment.NewLine}{ex}");
            MessageBox.Show(manWindow, $"{dirInfo.FullName}{Environment.NewLine}{ex}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Log.Information($"End Load {dirInfo.Name} ...");
        }

        return videoEnties;
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
    private async Task ProcessForDirsAsync(DirectoryInfo dirInfo)
    {
        Log.Debug($"Start Process {dirInfo.Name} ...");

        var files = dirInfo.GetFiles();
        var videoFiles = files.Where(f => videoExt.Contains(f.Extension.ToLower())).ToList();
        var videoStoreFiles = videoFiles.Where(m => m.Length >= videoMaxMbSize).ToList();
        if (videoStoreFiles?.Any() ?? false)
        {
            Log.Debug($"Global video files added .");

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

        Log.Debug($"End Process {dirInfo.Name} .");
    }

    /// <summary>
    /// 异步处理所有视频文件。
    /// </summary>
    /// <returns>
    /// 表示异步操作的任务。
    /// </returns>
    /// <remarks>
    /// 此方法首先将所有的视频文件分割成等大小的批次，然后为每个批次创建一个新的任务来处理。
    /// 所有的任务都是并行运行的，以提高处理速度。当所有的任务都完成后，这个方法就结束。
    /// </remarks>
    private async Task ProcessAllVideosAsync()
    {
        Log.Information($"Start Process Videos ...");

        var taskCount = 1;
        var batchSize = this.videoFiles.Count / taskCount;
        batchSize = batchSize <= 0 ? 1 : batchSize;

        var array = this.videoFiles.Chunk(batchSize).ToList();
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
                this.videoCollection.Add(VideoEntry);
                this.dicVideos[VideoEntry.VideoPath] = VideoEntry;
                var json = JsonConvert.SerializeObject(this.videoCollection);

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
    /// 异步处理一组视频文件。
    /// </summary>
    /// <returns>
    /// 表示异步操作的任务。
    /// </returns>
    /// <remarks>
    /// 该方法使用LibVLC库初始化一个MediaPlayer对象。然后遍历集合中的每个视频文件，
    /// 对于每个视频，它在规定的时间间隔内抓取帧并将其保存为图像。它为每个视频创建一个带有元数据和快照的实体，
    /// 并将这些实体序列化为JSON文件。
    /// </remarks>
    private async Task ProcessVideosAsync()
    {
        Log.Information($"Start Process Videos , Video count :{this.videoFiles.Count}.");

        var picCount = 10;
        var dirInfo = new DirectoryInfo(this.dirPath);
        using LibVLC? libVLC = new LibVLC();
        using var mediaPlayer = new MediaPlayer(libVLC); // 播放器

        var (datapath, jsonfile, name) = this.GetDataDirPath();

        if (!Directory.Exists(datapath))
            Directory.CreateDirectory(datapath);

        foreach (var item in this.videoFiles)
        {
            if (dicVideos?.ContainsKey(item.FullName) ?? false)
            {
                Log.Debug($"{item.Name} Video already exists, processed.");
                continue;
            }

            try
            {
                Log.Debug($"Process Video {item.Name}.");
                var times = new List<long>(); // 截图时间点
                var images = new List<string>(); // 截图文件
                var length = await this.ParseMediaAsync(libVLC, item);
                var media = new Media(libVLC, item.FullName, FromType.FromPath); // 视频文件
                var interval = length / picCount; // 截图时间间隔
                mediaPlayer.Media = media; // 设置视频文件
                mediaPlayer.EncounteredError += (s, e) => Log.Error($"MediaPlayer Error: {e}");

                for (int i = 0; i < picCount; i++)
                    times.Add(interval * i); // 添加播放时间  

                times.RemoveAt(0); // 移除第一个时间点
                times.RemoveAt(times.Count - 1); // 移除最后一个时间点

                mediaPlayer.Play();
                mediaPlayer.ToggleMute(); // 静音
                await Task.Delay(500);

                while (mediaPlayer.State != VLCState.Playing)
                {
                    Thread.Sleep(500);

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
                    await Task.Delay(100); // 等待截图完成
                    mediaPlayer.TakeSnapshot(0, snapshot, 0, 0); // 截图
                    await Task.Delay(100); // 等待截图完成
                }

                VideoEntry.Snapshots = new ObservableCollection<string>(images);
                this.videoCollection.Add(VideoEntry);
                this.dicVideos[VideoEntry.VideoPath] = VideoEntry;
                var json = JsonConvert.SerializeObject(this.videoCollection);
                await File.WriteAllTextAsync(jsonfile, json);
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {item.FullName}{Environment.NewLine}{ex}");
            }
            finally
            {
                mediaPlayer?.Stop();
                await Task.Delay(100);
            }
        }

        mediaPlayer?.Dispose();
        Log.Information($"End Process Videos , Video count :{this.videoFiles.Count}.");
    }

    /// <summary>
    /// 异步处理单个视频文件。
    /// </summary>
    /// <param name="enty">表示视频实体的对象。</param>
    /// <returns>
    /// 表示异步操作的任务。
    /// </returns>
    /// <remarks>
    /// 该方法使用LibVLC库初始化一个MediaPlayer对象，并打开指定的视频文件。然后在规定的时间间隔内抓取帧并将其保存为图像。
    /// 该方法还更新一个已存在的视频实体，并将其序列化为JSON文件。
    /// </remarks>
    private async Task ProcessVideoAsync(VideoEntry enty)
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
            var (datapath, jsonfile, name) = this.GetDataDirPath();
            mediaPlayer.Media = media; // 设置视频文件
            mediaPlayer.EncounteredError += (s, e) => { Log.Error($"Error: {e}"); };

            for (int i = 0; i < picCount; i++)
                times.Add(interval * i); // 添加播放时间  

            times.RemoveAt(0); // 移除第一个时间点
            times.RemoveAt(times.Count - 1); // 移除最后一个时间点

            mediaPlayer.Play();
            mediaPlayer.ToggleMute(); // 静音
            await Task.Delay(500);

            while (mediaPlayer.State != VLCState.Playing)
                await Task.Delay(500);

            enty.Caption = Path.GetFileNameWithoutExtension(enty.VideoPath); // 视频标题
            enty.Length = item.Length / 1024 / 1024; // 视频大小
            enty.VideoPath = item.FullName; // 视频路径
            enty.MidifyTime = item.LastWriteTime; // 修改时间
            enty.VideoDir = datapath;

            foreach (var time in times)
            {
                var picName = $"{Guid.NewGuid()}.png";
                var snapshot = Path.Combine(datapath, picName);
                images.Add(snapshot);

                mediaPlayer.Time = time; // 设置播放时间
                await Task.Delay(500);// 等待截图完成
                mediaPlayer.TakeSnapshot(0, snapshot, 0, 0); // 截图  
            }

            this.DeleteVideoImages(enty);
            enty.Snapshots?.Clear();

            foreach (var img in images)
            {
                enty.Snapshots.Add(img);
            }

            await this.SaveAsync();
        }
        catch (Exception ex)
        {
            Log.Error($"Error: {enty.VideoPath}{Environment.NewLine}{ex}");
        }
        finally
        {
            mediaPlayer.Stop();
            mediaPlayer.Dispose();
        }
    }

    /// <summary>
    /// 删除视频图片
    /// </summary>
    /// <param name="enty">视频实体</param>
    private void DeleteVideoImages(VideoEntry enty)
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
                Log.Error($"File Del Error:{item}{Environment.NewLine}{ex}");
            }
        }
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

    /// <summary>
    /// 保存视频实体的集合到一个JSON文件中。
    /// </summary>
    /// <param name="dirInfo">代表目标目录的对象。</param>
    /// <remarks>
    /// 此方法首先检查两个可能的视频实体集合 'Videos' 和 'videoCollection'，如果其中任何一个不为空，则将其序列化为 JSON 字符串。
    /// 然后，它检查数据路径是否存在，如果不存在则创建它。最后，如果目标 JSON 文件已经存在，它会先删除该文件，然后将 JSON 字符串写入新的文件中。
    /// </remarks>
    private void Save(DirectoryInfo dirInfo)
    {
        var (datapath, jsonfile, name) = this.GetDataDirPath();

        var json = string.Empty;

        if (this.Videos?.Any() ?? false)
            json = JsonConvert.SerializeObject(this.Videos, Formatting.Indented);
        else if (this.videoCollection?.Any() ?? false)
            json = JsonConvert.SerializeObject(this.videoCollection, Formatting.Indented);

        if (!Directory.Exists(datapath))
            Directory.CreateDirectory(datapath);

        if (File.Exists(jsonfile))
            File.Delete(jsonfile);

        File.WriteAllText(jsonfile, json);
    }

    /// <summary>
    /// 将当前的视频实体列表转换为同步集合。
    /// </summary>
    /// <remarks>
    /// 如果当前的视频实体列表不为空，那么它将遍历视频实体列表，将每个视频实体添加到视频集合中。
    /// </remarks>
    private void VideosToSynchronizedCollection()
    {
        if (this.Videos?.Any() ?? false)
        {
            foreach (var item in this.Videos)
            {
                this.videoCollection?.Add(item);
            }
        }
    }

    /// <summary>
    /// 清除已存在的视频文件。
    /// </summary> 
    private void ClearExists()
    {
        var count = this.videoFiles.Count;

        for (int i = count - 1; i >= 0; i--)
        {
            var vfile = this.videoFiles[i];
            if (this.dicVideos.ContainsKey(vfile.FullName))
            {
                Log.Debug($"{vfile.Name} Video already exists, processed.");
                this.videoFiles.Remove(vfile);
            }
        }
    }

    /// <summary>
    /// 获取数据目录路径
    /// </summary>
    /// <returns>包含目录路径、文件路径和名称的元组</returns>
    private (string dir, string file, string name) GetDataDirPath()
    {
        var dirInfo = new DirectoryInfo(this.DirPath);
        var currDirInfo = new DirectoryInfo(AppContext.BaseDirectory);
        var dataDirPath = Path.Combine(currDirInfo.Parent.FullName, "data", dirInfo.Name);
        var name = $"data.json";
        var jsonfile = Path.Combine(dataDirPath, name);
        return (dataDirPath, jsonfile, name);
    }

    #endregion
}
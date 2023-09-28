using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using Newtonsoft.Json;
using Serilog;
using YExplorer;
using YExplorer.Models;

namespace YExplorer.ViewModels;

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
        this.taskCount = AppSettingsUtils.Default.TaskCount;
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
    private readonly decimal videoMaxMbSize = 110 * 1024 * 1024;

    #endregion

    #region Fields

    /// <summary>
    /// 任务数量
    /// </summary>
    private int taskCount = 1;

    /// <summary>
    /// 数据存储目录
    /// </summary>
    private string dataPath;

    /// <summary>
    /// 播放器路径
    /// </summary>
    private string playerPath;

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

    /// <summary>
    /// 当前索引
    /// </summary>
    private int currentIndex = 0;
    #endregion

    #region Property

    /// <summary>
    /// 宽度
    /// </summary>
    [ObservableProperty]
    private double windowWidth = Application.Current.MainWindow.Width - 50;

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
            MessageBox.Show($"{ex}");
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
                this.LoadNextItem(this.allVideos.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}");
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

            await this.ProcessVideosAsync(files, this.taskCount);

            this.LoadNextItem(this.loadCount);

            Log.Information($"Process videos End。");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}");
        }
    }

    /// <summary>
    /// 异步清理原始目录。
    /// </summary>
    /// <returns>
    /// 表示异步操作的任务。
    /// </returns>
    /// <remarks>
    /// 此方法首先获取存储数据的目录路径，然后调用`DeleteAll`方法删除所有数据。
    /// </remarks>
    [RelayCommand]
    public async Task DeleteOriginalAsync()
    {
        try
        {
            var uri = this.SelectedDir;
            var dirInfo = new DirectoryInfo(uri);
            DeleteOriginalDir(dirInfo);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}");
        }
    }

    /// <summary>
    /// 打开日志目录。
    /// </summary>
    /// <remarks>
    /// 此方法首先获取当前应用程序域的基目录，然后构造日志目录的路径。最后，它使用Windows资源管理器打开日志目录。
    /// </remarks>
    [RelayCommand]
    public async Task OpenLogDirAsync()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

            // Running on Windows
            Process.Start("explorer.exe", path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}");
        }
    }

    /// <summary>
    /// 打开数据目录。
    /// </summary>
    /// <remarks>
    /// 此方法首先获取当前应用程序域的基目录，然后构造数据目录的路径。最后，它使用Windows资源管理器打开数据目录。
    /// </remarks>
    [RelayCommand]
    public async Task OpenDataDirAsync()
    {
        try
        {
            // Running on Windows
            Process.Start("explorer.exe", this.dataPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}");
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
    public async Task ClearInvalidDataDirAsync()
    {
        try
        {
            var delCount = 0;
            var dirInfo = new DirectoryInfo(GetDataDirPath().dir);
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

            MessageBox.Show($"清理数据资源完成, 清理文件 {delCount} 个。");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}");
        }
    }

    /// <summary>
    /// 清除指定目录下的所有数据。
    /// </summary>
    /// <remarks>
    /// 此方法首先获取数据目录的路径，然后检查该目录是否存在。如果存在，它会删除该目录及其所有子目录和文件。
    /// </remarks>
    [RelayCommand]
    public async Task ClearDataAsync()
    {
        try
        {
            var delCount = 0;
            this.allVideos.Clear();
            this.Videos.Clear();
            var dataConf = this.GetDataDirPath();
            var dataDirPath = dataConf.dir;
            var dataDir = new DirectoryInfo(dataDirPath);

            if (dataDir.Exists)
            {
                dataDir.Delete(true);
            }

            MessageBox.Show($"清理数据资源完成.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}");
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
    public async Task ScrollChanged(dynamic parameter)
    {
        try
        {
            LoadNextItem(this.loadCount);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}");
        }
    }

    #region Details

    /// <summary>
    /// 播放指定路径的视频文件。
    /// </summary>
    /// <param name="param">表示文件路径的对象。</param>
    /// <remarks>
    /// 此方法首先将传入的参数转换为字符串路径，然后检查路径是否为空。如果路径不为空，那么它会使用PotPlayer播放器打开并播放该路径的视频文件，然后增加该视频的播放次数。
    /// </remarks> 
    [RelayCommand]
    public async Task PlayAsync(object param)
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
            MessageBox.Show($"{ex}");
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
    public async Task FolderAsync(object param)
    {
        try
        {
            string path = param as string;
            var dirPath = Path.GetDirectoryName(path);

            // Running on Windows
            Process.Start("explorer.exe", dirPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}");
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
    public async Task DeleteAsync(object param)
    {
        try
        {
            if (param is VideoEntry video)
            {
                if (File.Exists(video.VideoPath))
                    File.Delete(video.VideoPath);

                this.Videos.Remove(video);
                this.allVideos.Remove(video);
                await this.SaveDataAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}");
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
    public async Task DeleteFolderAsync(object param)
    {
        try
        {
            if (param is VideoEntry video)
            {
                var result = MessageBox.Show($"Are you sure you want to delete the folder {video.VideoPath}?", caption: "Question", MessageBoxButton.OKCancel);

                if (result == MessageBoxResult.Cancel)
                    return;

                var dirName = Path.GetDirectoryName(video.VideoPath);

                var videos = this.Videos.Where(m => m.VideoPath.StartsWith(dirName)).ToList();
                foreach (var item in videos)
                {
                    if (File.Exists(item.VideoPath))
                        File.Delete(item.VideoPath);

                    this.Videos.Remove(item);
                    this.allVideos.Remove(item);
                }

                if (Directory.Exists(dirName))
                    Directory.Delete(dirName, true);

                await this.SaveDataAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            MessageBox.Show($"{ex}", "Error");
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
    public async Task ResetVideoAsync(object param)
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
            MessageBox.Show($"{ex}", "Error");
        }
    }

    #endregion
    #endregion

    #region Private

    /// <summary>
    /// 保存视频实体的集合到一个JSON文件中。
    /// </summary>
    /// <remarks>
    /// 此方法首先检查两个可能的视频实体集合 'Videos' 和 'videoCollection'，如果其中任何一个不为空，则将其序列化为 JSON 字符串。
    /// 然后，它检查数据路径是否存在，如果不存在则创建它。最后，如果目标 JSON 文件已经存在，它会先删除该文件，然后将 JSON 字符串写入新的文件中。
    /// </remarks>
    private async Task SaveDataAsync()
    {
        var (datapath, jsonfile, name) = this.GetDataDirPath();

        var json = string.Empty;

        if (this.allVideos?.Any() ?? false)
            json = JsonConvert.SerializeObject(this.allVideos, Formatting.Indented);

        if (!Directory.Exists(datapath))
            Directory.CreateDirectory(datapath);

        if (File.Exists(jsonfile))
            File.Delete(jsonfile);

        await File.WriteAllTextAsync(jsonfile, json);
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

        try
        {
            this.allVideos = new();
            var dataConf = this.GetDataDirPath();
            var dataDirPath = dataConf.dir;
            var jsonfile = dataConf.file;
            if (File.Exists(jsonfile))
            {
                var json = await File.ReadAllTextAsync(jsonfile);
                var _videos = JsonConvert.DeserializeObject<List<VideoEntry>>(json);
                _videos = _videos.OrderByDescending(x => x.MidifyTime).ToList();

                foreach (var video in _videos)
                {
                    if (video.Snapshots?.Any() ?? false)
                    {
                        var notExistsCount = video.Snapshots.Count(m => !File.Exists(m));
                        if (notExistsCount < video.Snapshots.Count / 3)
                        {
                            this.allVideos?.Add(video);
                        }
                    }
                }
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
            MessageBox.Show($"{dirInfo.FullName}{Environment.NewLine}{ex}");
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
            if (!(this.allVideos?.Any() ?? false))
                return;

            var entries = this.allVideos.Skip(this.Videos.Count).Take(count);

            if (entries?.Any() ?? false)
            {
                foreach (var video in entries)
                    this.Videos.Add(video);
            }
        }
    }

    #region Process

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
                var video = this.dicVideos[vfile.FullName];
                if (video.Snapshots?.Any() ?? false)
                {
                    var notExistsCount = video.Snapshots.Count(m => !File.Exists(m));
                    if (notExistsCount > video.Snapshots.Count / 3)
                    {
                        this.allVideos?.Remove(video);
                        this.Videos?.Remove(video);
                        this.dicVideos?.Remove(vfile.FullName, out var video1);
                    }
                    else
                    {
                        files.Remove(vfile);
                    }
                }
                else
                {
                    this.allVideos?.Remove(video);
                    this.Videos?.Remove(video);
                    this.dicVideos?.Remove(vfile.FullName, out var video1);
                }

                Log.Debug($"{vfile.Name} Video already exists, processed.");
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

            await this.SaveDataAsync();
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

    #endregion

    #region 清理

    /// <summary>
    /// 删除指定目录及其所有子目录下的部分文件。
    /// </summary>
    /// <param name="dirInfo">表示目标目录的对象。</param>
    /// <remarks>
    /// 此方法首先获取指定目录下的所有文件，并根据文件的扩展名和大小将其分为不同的类别。然后，它会删除满足特定条件的文件，
    /// 包括图片文件、小于某个阈值的视频文件，以及除了图片、视频和特定文件之外的所有其他文件。最后，这个方法递归地对每一个子目录执行同样的操作，
    /// 如果一个目录中没有大于某个阈值的视频文件，那么这个目录会被删除。
    /// </remarks>
    private void DeleteOriginalDir(DirectoryInfo dirInfo)
    {
        Log.Information($"Start del {dirInfo.FullName} ...");
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
                DeleteOriginalDir(item);
            }
        }
        else if (!(videoStoreFiles?.Any() ?? false))
        {
            dirInfo.Delete(true);
        }

        Log.Information($"End del {dirInfo} .");
    }

    #endregion

    #endregion
}

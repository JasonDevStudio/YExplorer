using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
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
            var dirInfo = new DirectoryInfo(this.SelectedDir);
            this.allVideos = await this.LoadDirAsync(dirInfo);

            if (this.allVideos?.Any() ?? false)
            {
                this.LoadNextItem(20);
            }
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
        if (this.dataEnumerator == null) return;

        if (count == -1)
            this.Videos = new ObservableCollection<VideoEntry>(this.allVideos);

        for (int i = 0; i < count; i++)
        {
            if (this.dataEnumerator.MoveNext())
                this.Videos.Add((VideoEntry)this.dataEnumerator.Current);
        }
    }

    #endregion
}

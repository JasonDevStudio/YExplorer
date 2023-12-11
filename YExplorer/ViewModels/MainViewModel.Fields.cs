using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Emgu.CV;
using Emgu.CV.Stitching;
using Emgu.CV.Structure;
using HandyControl.Controls;
using LibVLCSharp.Shared;
using Microsoft.Win32;
using Newtonsoft.Json;
using Serilog;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using YExplorer.Models;

namespace YExplorer.ViewModels;

/// <summary>
/// MainViewModel 类，它继承自 ObservableObject 类。
/// </summary>
/// <remarks>
/// 这个类是 ViewModel 部分，它处理视图中的业务逻辑，并通过数据绑定将数据从 Model 传递到 View。
/// 在这个类中，可能会定义一些属性和命令，这些属性和命令绑定到视图的控件，以实现界面的各种功能。
/// </remarks>
partial class MainViewModel
{
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
    [ObservableProperty]
    private int taskCount = 1;

    /// <summary>
    /// 任务数量集合
    /// </summary>
    [ObservableProperty]
    private List<int> taskCounts = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

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
    /// 首次加载数量
    /// </summary>
    private int firstLoadCount = 15;

    /// <summary>
    /// 以字符串为键，VideoEntry为值的线程安全字典
    /// </summary>
    private ConcurrentDictionary<string, VideoEntry> dicVideos = new();

    /// <summary>
    /// 是否全量加载
    /// </summary>
    private bool isAllLoad = false;

    /// <summary>
    /// 文件选择器
    /// </summary>
    private FileDialog fileDialog = new OpenFileDialog()
    {
        DefaultExt = ".7z",
        InitialDirectory = @"X:\10_Backup",
        Filter = "7z files (*.7z)|*.7z|All files (*.*)|*.*",
    };

    private bool isLoadData = false;

    private List<FileInfo> allImages = new List<FileInfo>();

    /// <summary>
    /// 图片数据
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> images = new();

    /// <summary>
    /// 显示关闭 视频数据区
    /// </summary>
    [ObservableProperty]
    private Visibility videoVisibility = Visibility.Visible;

    /// <summary>
    /// 显示关闭 图片数据区
    /// </summary>
    [ObservableProperty]
    private Visibility picVisibility = Visibility.Collapsed;

    #endregion

    #region Property

    /// <summary>
    /// 宽度
    /// </summary>
    [ObservableProperty]
    private double windowWidth = System.Windows.Application.Current.MainWindow.Width - 50;

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

    /// <summary>
    /// 是否忙碌
    /// </summary>
    [ObservableProperty]
    private bool isBusy = false;

    #endregion
}

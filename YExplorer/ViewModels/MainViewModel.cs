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
public partial class MainViewModel : ObservableObject
{
    /// <summary>
    /// 初始化MainViewModel类的新实例。
    /// </summary>
    public MainViewModel()
    {
        var _dir_99 = new DirectoryInfo(@"\\192.168.10.2\99_资源收藏\01_成人资源");
        var _dir_98 = new DirectoryInfo(@"\\192.168.10.2\98_资源收藏\01_成人资源");
        var dirs_99 = _dir_99.GetDirectories();
        var dirs_98 = _dir_98.GetDirectories();
        var allDirs = dirs_99.Concat(dirs_98).OrderByDescending(m => m.CreationTime).Select(m => m.FullName);
        this.DirPaths = new ObservableCollection<string>(allDirs);
        this.dataPath = AppSettingsUtils.Default.WinDataPath;
        this.playerPath = AppSettingsUtils.Default.WinPlayerPath;
        this.taskCount = AppSettingsUtils.Default.TaskCount;
        VideoEntry.SaveCmd = this.SaveDataCommand;
    }

    #region Load

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
            this.IsBusy = true;
            this.Videos.Clear();
            this.allVideos.Clear();
            this.loadCount = 1;
            this.isAllLoad = false;
            this.isLoadData = true;
            this.PicVisibility = Visibility.Collapsed;
            this.VideoVisibility = Visibility.Visible;
            var dirInfo = new DirectoryInfo(this.SelectedDir);
            this.allVideos = await this.LoadDirAsync(dirInfo);

            if (this.allVideos?.Any() ?? false)
            {
                this.LoadNextItem(firstLoadCount);
            }

            Growl.Success("加载完成");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            Growl.Error($"{ex}");
        }
        finally
        {
            this.isLoadData = false;
            this.IsBusy = false;
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
            this.IsBusy = true;
            this.Videos.Clear();
            this.allVideos.Clear();
            this.loadCount = 50;
            this.isLoadData = true;
            this.isAllLoad = true;
            this.PicVisibility = Visibility.Collapsed;
            this.VideoVisibility = Visibility.Visible;
            await Task.Run(async () =>
            {
                var dirInfo = new DirectoryInfo(this.SelectedDir);
                this.allVideos = await this.LoadDirAsync(dirInfo);
            });

            if (this.allVideos?.Any() ?? false)
            {
                this.LoadNextItem(this.allVideos.Count);
            }

            Growl.Success("全部加载完成");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            Growl.Error($"{ex}");
        }
        finally
        {
            this.isLoadData = false;
            this.IsBusy = false;
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
            Growl.Error($"{ex}");
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
            Growl.Error($"{ex}");
        }
    }

    /// <summary>
    /// 处理滚动事件。
    /// </summary>
    /// <param name="args">包含参数的动态对象。</param> 
    [RelayCommand]
    public async Task OrderAsync(string args)
    {
        try
        {
            this.IsBusy = true;

            await Task.Run(() =>
            {
                switch (args)
                {
                    case "1":
                        this.allVideos = this.allVideos.OrderBy(m => m.MidifyTime).ToList();
                        break;
                    case "2":
                        this.allVideos = this.allVideos.OrderByDescending(m => m.MidifyTime).ToList();
                        break;
                    case "3":
                        this.allVideos = this.allVideos.OrderByDescending(m => m.PlayCount).ThenByDescending(m => m.MidifyTime).ToList();
                        break;
                    case "4":
                        this.allVideos = this.allVideos.OrderByDescending(m => m.PlayCount).ThenBy(m => m.MidifyTime).ToList();
                        break;
                    case "5":
                        this.allVideos = this.allVideos.OrderByDescending(m => m.Evaluate).ThenBy(m => m.MidifyTime).ToList();
                        break;
                    case "0":
                    default:
                        this.allVideos = this.allVideos.OrderByDescending(m => m.Evaluate).ThenByDescending(m => m.MidifyTime).ToList();
                        break;
                }
            });

            this.Videos.Clear();

            if (this.isAllLoad)
                this.LoadNextItem(this.allVideos.Count);
            else
                this.LoadNextItem(firstLoadCount);

            Growl.Success("排序完成");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            Growl.Error($"{ex}");
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    #endregion

    #region Scroll

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
            Growl.Error($"{ex}");
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
    public async Task PicScrollChanged(dynamic parameter)
    {
        try
        {
            // 检查是否已经滚动到底部
            bool isAtBottom = parameter.VerticalOffset >= parameter.ScrollableHeight;

            if (isAtBottom)
            {
                // 滚动条已经到达底部的逻辑处理

                this.IsBusy = true;
                var pics = await this.LoadPics(this.SelectedDir);

                foreach (var item in pics)
                {
                    this.Images.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            Growl.Error($"{ex}");
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    /// <summary>
    /// 滚动条置顶
    /// </summary>
    [RelayCommand]
    private async void ScrollToAsync(string args)
    {
        switch (args)
        {
            case "top":
                // 使用 Messenger 类发送一个消息，通知 View 执行滚动到顶部的方法
                // Messenger 是一个简单的消息传递机制，可以在不同的类之间传递消息
                // 你可以使用任何你喜欢的消息传递机制，例如事件或委托等
                WeakReferenceMessenger.Default.Send("ScrollToTop");
                break;
            case "bottom":
                // 使用 Messenger 类发送一个消息，通知 View 执行滚动到顶部的方法
                // Messenger 是一个简单的消息传递机制，可以在不同的类之间传递消息
                // 你可以使用任何你喜欢的消息传递机制，例如事件或委托等
                WeakReferenceMessenger.Default.Send("ScrollToBottom");
                break;
        }
    }

    #endregion

    #region Process

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
            var tmpDics = this.allVideos.DistinctBy(m => m.VideoPath)?.ToDictionary(mm => mm.VideoPath) ?? new();
            var dirInfo = new DirectoryInfo(this.SelectedDir);
            this.dicVideos = new ConcurrentDictionary<string, VideoEntry>(tmpDics);

            var files = await this.ProcessForDirsAsync(dirInfo);

            Log.Information($"Scan videos count {files.Count}");

            files = this.ClearExists(files);

            Log.Information($"Filterd videos count {files.Count}");

            await this.ProcessVideosAsync(files, this.TaskCount);

            this.LoadNextItem(this.loadCount);

            Log.Information($"Process videos End。");
            Growl.Success($"Process videos End。");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            Growl.Error($"{ex}");
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
            this.IsBusy = true;

            await Task.Run(() =>
            {
                var uri = this.SelectedDir;
                var dirInfo = new DirectoryInfo(uri);
                DeleteOriginalDir(dirInfo);
            });

            Growl.Success("清理原始目录完成。");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            Growl.Error($"{ex}");
        }
        finally
        {
            this.IsBusy = false;
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
            this.IsBusy = true;
            var delCount = 0;
            var _videos = this.Videos?.ToList();
            await Task.Run(() =>
            {
                var dirInfo = new DirectoryInfo(GetDataDirPath().dir);
                var dataConf = this.GetDataDirPath();
                var dataDirPath = dataConf.dir;
                var dicFiles = new Dictionary<string, VideoEntry>();
                var dataDir = new DirectoryInfo(dataDirPath);

                if (_videos?.Any() ?? false)
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
            });

            Growl.Success($"清理数据资源完成, 清理文件 {delCount} 个。");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            Growl.Error($"{ex}");
        }
        finally
        {
            this.IsBusy = false;
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
            this.IsBusy = true;
            var delCount = 0;
            this.Videos.Clear();

            await Task.Run(() =>
            {
                this.allVideos.Clear();

                var dataConf = this.GetDataDirPath();
                var dataDirPath = dataConf.dir;
                var dataDir = new DirectoryInfo(dataDirPath);

                if (dataDir.Exists)
                {
                    dataDir.Delete(true);
                }
            });

            Growl.Success($"清理数据资源完成.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            Growl.Error($"{ex}");
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    /// <summary>
    /// 备份/还原 数据目录
    /// </summary>
    /// <param name="args">参数</param>
    /// <returns>Task</returns>
    [RelayCommand]
    public async Task ZipDirectoryAsync(string args)
    {
        switch (args)
        {
            case "unzip":
                var dialogResult = fileDialog.ShowDialog();
                if (dialogResult == true)
                {
                    var zipFile = fileDialog.FileName;
                    var dirInfo = new DirectoryInfo(AppSettingsUtils.Default.WinDataPath);

                    if (dirInfo.Exists)
                        dirInfo.Delete(true);

                    dirInfo.Create();

                    await Task.Run(() => this.UnzipWith7Zip(zipFile, dirInfo.FullName));
                    Growl.Success("恢复完成");
                }
                break;
            case "zip":
                var zipPath = Path.Combine(AppSettingsUtils.Default.BackupPath, $"data_{DateTime.Now:yyyy_MM_dd_HH_mm}.7z");
                await Task.Run(() => this.ZipDirectoryWith7Zip(AppSettingsUtils.Default.WinDataPath, zipPath));
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 保存视频实体的集合到一个JSON文件中。
    /// </summary>
    /// <remarks>
    /// 此方法首先检查两个可能的视频实体集合 'Videos' 和 'videoCollection'，如果其中任何一个不为空，则将其序列化为 JSON 字符串。
    /// 然后，它检查数据路径是否存在，如果不存在则创建它。最后，如果目标 JSON 文件已经存在，它会先删除该文件，然后将 JSON 字符串写入新的文件中。
    /// </remarks>
    [RelayCommand]
    public async Task SaveDataAsync()
    {
        if (isLoadData)
            return;

        (bool success, string msg) = (true, string.Empty);

        try
        {
            var (datapath, jsonfile, name) = this.GetDataDirPath();
            var json = string.Empty;

            if (this.allVideos?.Any() ?? false)
                json = JsonConvert.SerializeObject(this.allVideos, Formatting.Indented);

            if (string.IsNullOrWhiteSpace(json))
            {
                success = false;
                msg = "没有数据需要保存";
            }
            else
            {
                if (!Directory.Exists(datapath))
                    Directory.CreateDirectory(datapath);

                if (File.Exists(jsonfile))
                    File.Delete(jsonfile);

                if (success)
                    await File.WriteAllTextAsync(jsonfile, json);

                success = true;
                msg = "保存完成";
            }
        }
        catch (Exception ex)
        {
            Growl.Error(ex.ToString());
        }

        if (success)
            Growl.Success(msg);
        else
            Growl.Warning(msg);
    }

    /// <summary>
    /// 删除不存在的视频的图片。
    /// </summary>
    [RelayCommand]
    public async Task DeleteVideoNotExistsImagesAsync()
    {
        if (this.allVideos?.Any() ?? false)
        {
            this.DeleteVideoNotExistsImages(this.allVideos);
            await this.SaveDataAsync();
        }
    }

    #endregion

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
            Growl.Error($"{ex}");
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
            Growl.Error($"{ex}");
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
            this.IsBusy = false;
            if (param is VideoEntry video)
            {
                await Task.Run(async () =>
                {
                    if (File.Exists(video.VideoPath))
                        File.Delete(video.VideoPath);

                    this.allVideos.Remove(video);
                    await this.SaveDataAsync();
                });

                this.Videos.Remove(video);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            Growl.Error($"{ex}");
        }
        finally
        {
            this.IsBusy = false;
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
                var dirName = Path.GetDirectoryName(video.VideoPath);

                if (this.SelectedDir == dirName)
                {
                    Growl.Warning($"根目录不允许直接删除文件夹");
                    return;
                }

                var result = HandyControl.Controls.MessageBox.Show($"Are you sure you want to delete the folder {video.VideoPath}?", caption: "Question", MessageBoxButton.OKCancel);

                if (result == MessageBoxResult.Cancel)
                    return;

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
            Growl.Error($"{ex}");
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
            Growl.Error($"{ex}");
        }
    }

    #endregion

    #region Pics

    /// <summary>
    /// 加载图片
    /// </summary>
    [RelayCommand]
    private async Task LoadPicAsync()
    {
        try
        {
            this.IsBusy = true;
            this.Images.Clear();
            this.PicVisibility = Visibility.Visible;
            this.VideoVisibility = Visibility.Collapsed;
            this.allImages.Clear();
            var pics = await this.LoadPics(this.SelectedDir);
            this.Images = new ObservableCollection<string>(pics);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{MethodBase.GetCurrentMethod().Name} Is Error");
            Growl.Error($"{ex}");
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    #endregion
}

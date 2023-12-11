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
    #region Pic

    /// <summary>
    /// 图片列表
    /// </summary>
    /// <param name="path"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public async Task<List<string>> LoadPics(string path, int count = 10)
    {
        return await Task.Run(() =>
        {
            if (!this.allImages?.Any() ?? false)
            {
                var exts = new List<string> { "*.jpg", "*.png", "*.gif", "*.bmp" };
                var pics = new List<FileInfo>();
                var filtedPics = new List<FileInfo>();
                var dirInfo = new DirectoryInfo(this.SelectedDir);

                foreach (var ext in exts)
                {
                    var files = dirInfo.GetFiles(ext, SearchOption.AllDirectories);
                    pics.AddRange(files);
                }

                if (pics?.Any() ?? false)
                {
                    foreach (var pic in pics)
                    {
                        if (pic.Length > 1048576)
                        {
                            filtedPics.Add(pic);
                        }
                    }
                }

                this.allImages = filtedPics;
            }

            return this.allImages.Select(m => m.FullName).Skip(this.Images?.Count ?? 0).Take(count).ToList();
        });
    }

    #endregion

    #region Load

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
            var filterdCount = 0;
            if (File.Exists(jsonfile))
            {
                var json = await File.ReadAllTextAsync(jsonfile);
                var _videos = JsonConvert.DeserializeObject<List<VideoEntry>>(json);
                _videos = _videos?.OrderByDescending(x => x.Evaluate)
                    .ThenByDescending(x => x.MidifyTime ?? DateTime.MaxValue).ToList();

                if (_videos?.Any() ?? false)
                {
                    foreach (var video in _videos)
                    {
                        if (video.Snapshots?.Any() ?? false)
                        {
                            var notExistsCount = video.Snapshots.Count(m => !File.Exists(m) || IsImageBlack(m));
                            if (notExistsCount < video.Snapshots.Count / 2)
                                this.allVideos?.Add(video);
                            else
                                filterdCount++;
                        }
                    }
                }
            }

            if (this.allVideos?.Any() ?? false)
            {
                foreach (var item in this.allVideos)
                {
                    item.Dir = Path.GetDirectoryName(item.VideoPath);
                    item.Dir = item.Dir.Replace(this.SelectedDir, string.Empty).Trim('\\');
                }
            }

            if (filterdCount > 0)
                Growl.Warning($"Filterd {filterdCount} videos.");
        }
        catch (Exception ex)
        {
            Log.Information($"Error: {dirInfo.FullName}{Environment.NewLine}{ex}");
            Growl.Error($"{dirInfo.FullName}{Environment.NewLine}{ex}");
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

    #endregion

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
            tasks.Add(ProcessVideosAsync(array[i]));

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
        await Task.Run(async () =>
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
                        var picName = $"{Guid.NewGuid()}.jpg";
                        var snapshot = Path.Combine(datapath, picName);                         
                        mediaPlayer.Time = time; // 设置播放时间
                        await Task.Delay(200); // 等待截图完成
                        mediaPlayer.TakeSnapshot(0, snapshot, 0, height: 0); // 截图
                        await Task.Delay(500); // 等待截图完成
                        CompressAsPng(snapshot);

                        if(File.Exists(snapshot))
                            images.Add(snapshot);
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
        });
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
            await Task.Delay(200);

            while (mediaPlayer.State != VLCState.Playing)
                await Task.Delay(500);

            enty.Caption = Path.GetFileNameWithoutExtension(enty.VideoPath); // 视频标题
            enty.Length = item.Length / 1024 / 1024; // 视频大小
            enty.VideoPath = item.FullName; // 视频路径
            enty.MidifyTime = item.LastWriteTime; // 修改时间
            enty.VideoDir = datapath;

            foreach (var time in times)
            {
                var picName = $"{Guid.NewGuid()}.jpg";
                var snapshot = Path.Combine(datapath, picName); 
                mediaPlayer.Time = time; // 设置播放时间
                await Task.Delay(200); // 等待截图完成
                mediaPlayer.TakeSnapshot(0, snapshot, 0, 0); // 截图
                await Task.Delay(500);
                CompressAsPng(snapshot);

                if (File.Exists(snapshot))
                    images.Add(snapshot);
            }

            this.DeleteVideoImages(enty);
            enty.Snapshots = new ObservableCollection<string>(images); // 截图文件 

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

    /// <summary>
    /// 删除不存在的视频的图片。
    /// </summary>
    /// <param name="videos">视频列表，包含需要检查和删除图片的视频信息。</param>
    /// <remarks>
    /// 此函数遍历提供的视频列表，对于每个视频，检查其对应的图片是否存在。
    /// 如果图片不存在，则将其从文件系统中删除。
    /// 这有助于保持文件系统的整洁，防止无用的图片占用空间。
    /// </remarks>
    private void DeleteVideoNotExistsImages(List<VideoEntry> videos)
    {
        foreach (var item in videos)
        {
            for (global::System.Int32 i = item.Snapshots.Count - (1); i >= 0; i--)
            {
                try
                {
                    if (!File.Exists(item.Snapshots[i]))
                        item.Snapshots.RemoveAt(i);
                }
                catch (Exception ex)
                {
                    Log.Error($"File Del Error:{item.Snapshots[i]}{Environment.NewLine}{ex}");
                }
            } 
        }
    }

    /// <summary>
    /// 判断图像是否全黑
    /// </summary>
    /// <param name="imageFile">要检查的图像文件</param>
    /// <returns>如果图像全黑则返回true，否则返回false</returns>
    private bool IsImageBlack(string imageFile)
    {
        var image = new Mat(imageFile);

        // 获取图像的宽度和高度
        int width = image.Width;
        int height = image.Height;

        // 将图像转换为灰度图像
        Mat grayImage = new Mat();
        CvInvoke.CvtColor(image, grayImage, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);

        // 遍历每一个像素点
        Image<Gray, byte> img = grayImage.ToImage<Gray, byte>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Gray pixel = img[y, x];
                if (pixel.Intensity > 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 使用7z命令行工具压缩指定目录
    /// </summary>
    /// <param name="source">需要压缩的目录路径</param>
    /// <param name="target">目标压缩文件路径</param>
    /// <returns>如果成功返回true，否则返回false</returns>
    private bool ZipDirectoryWith7Zip(string source, string target)
    {
        try
        {
            // 初始化一个新的ProcessStartInfo实例
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "C:\\Program Files\\7-Zip\\7z.exe"; // 7z命令行工具的路径
            startInfo.Arguments = $"a -t7z -mx9 \"{target}\" \"{source}\\*\""; // 命令行参数
            startInfo.WindowStyle = ProcessWindowStyle.Normal; // 隐藏命令行窗口

            // 启动外部进程
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit(); // 等待进程完成

                // 检查进程是否正常退出
                if (process.ExitCode == 0)
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"{ex}");
            Growl.Error("An error occurred: " + ex);
        }

        return false;
    }

    /// <summary>
    /// 使用7z命令行工具解压指定压缩包到目标目录
    /// </summary>
    /// <param name="archivePath">压缩包路径</param>
    /// <param name="targetDirectory">目标解压目录</param>
    /// <returns>如果成功返回true，否则返回false</returns>
    private bool UnzipWith7Zip(string archivePath, string targetDirectory)
    {
        try
        {
            // 初始化一个新的ProcessStartInfo实例
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "C:\\Program Files\\7-Zip\\7z.exe"; // 7z命令行工具的路径
            startInfo.Arguments = $"x \"{archivePath}\" -o\"{targetDirectory}\" -y"; // 命令行参数
            startInfo.WindowStyle = ProcessWindowStyle.Hidden; // 隐藏命令行窗口

            // 启动外部进程
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit(); // 等待进程完成

                // 检查进程是否正常退出
                if (process.ExitCode == 0)
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"{ex}");
            Growl.Error("An error occurred: " + ex);
        }

        return false;
    }

    /// <summary>
    /// 无损压缩图片并保存为PNG格式
    /// </summary>
    /// <param name="inputPath">输入图片的路径</param>
    private void CompressAsPng(string inputPath)
    {
        if (!File.Exists(inputPath))
            return;

        // 加载原始图片
        // Load the original image
        using (var image = SixLabors.ImageSharp.Image.Load(inputPath))
        {
            // 设置PNG编码器选项
            // Set PNG encoder options
            var options = new JpegEncoder()
            {
                Quality = 90
            };

            if (File.Exists(inputPath))
                File.Delete(inputPath);

            // 保存为PNG格式
            // Save as PNG format
            var stream = File.OpenWrite(inputPath);
            image.Save(stream, options);
            stream.Close();
            stream.Dispose();
            image.Dispose();
        }
    }

    #endregion

    #region 清理

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
                    var notExistsCount = video.Snapshots.Count(m => !File.Exists(m) || IsImageBlack(m));
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
}
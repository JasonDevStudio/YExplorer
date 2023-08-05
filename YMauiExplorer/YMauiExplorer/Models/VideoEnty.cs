using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace YMauiExplorer.Models;

/// <summary>
/// 表示视频条目，包含诸如长度、播放次数、修改时间和快照等属性。
/// </summary>
public partial class VideoEntry : ObservableObject
{
    private string _caption;
    private string dir;
    private string videoPath;
    private long length;
    private long playCount = 0;
    public DateTime? midifyTime;
    private ObservableCollection<string> snapshots;
    private string videoDir;

    public string Dir
    {
        get => this.dir;
        set => this.SetProperty(ref this.dir, value);
    }

    /// <summary>
    /// 获取或设置视频的长度。
    /// </summary>
    public long Length
    {
        get => this.length;
        set => this.SetProperty(ref this.length, value);
    }

    /// <summary>
    /// 获取或设置视频的标题。
    /// </summary>
    public string Caption
    {
        get => this._caption;
        set => this.SetProperty(ref this._caption, value);
    }

    /// <summary>
    /// 获取或设置视频的目录。
    /// </summary>
    public string VideoDir
    {
        get => this.videoDir;
        set => this.SetProperty(ref this.videoDir, value);
    }

    /// <summary>
    /// 获取或设置视频的路径。
    /// </summary>
    public string VideoPath
    {
        get => this.videoPath;
        set => this.SetProperty(ref this.videoPath, value);
    }

    /// <summary>
    /// 获取或设置视频的播放次数。
    /// </summary>
    public long PlayCount
    {
        get => this.playCount;
        set => this.SetProperty(ref this.playCount, value);
    }

    /// <summary>
    /// 获取或设置视频的修改时间。
    /// </summary>
    public DateTime? MidifyTime
    {
        get => this.midifyTime;
        set => this.SetProperty(ref this.midifyTime, value);
    }

    /// <summary>
    /// 获取或设置视频的快照集合。
    /// </summary>
    public ObservableCollection<string> Snapshots
    {
        get => this.snapshots;
        set => this.SetProperty(ref this.snapshots, value);
    }
}

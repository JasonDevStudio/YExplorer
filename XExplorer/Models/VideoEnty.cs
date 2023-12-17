using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using XExplorer.DataModels;

namespace XExplorer.Models;

/// <summary>
/// 表示视频条目，包含诸如长度、播放次数、修改时间和快照等属性。
/// </summary>
public partial class VideoEntry : ObservableObject
{
    [JsonIgnore] public static Func<object, Task> SaveCmd { get; set; }

    /// <summary>
    /// 评分
    /// </summary>
    public int Evaluate
    {
        get => this.evaluate;
        set
        {
            if(this.evaluate == value) 
                return;
            
            this.SetProperty(ref this.evaluate, value);
            SaveCmd?.Invoke(this);
        }
    }

    /// <summary>
    /// 获取或设置视频的播放次数。
    /// </summary>
    public long PlayCount
    {
        get => this.playCount;
        set
        {
            if (this.playCount == value) 
                return;
            
            this.SetProperty(ref this.playCount, value);
            SaveCmd?.Invoke(this);
        }
    }

    [ObservableProperty] private long id;

    /// <summary>
    /// 视频的标题。
    /// </summary>
    [ObservableProperty] private string caption;

    /// <summary>
    /// 视频文件的存储目录。
    /// </summary>
    [ObservableProperty] private string dir;

    /// <summary>
    /// 视频文件的存储目录。
    /// </summary>
    [ObservableProperty] private string videoDir;

    /// <summary>
    /// 视频文件的完整路径。
    /// </summary>
    [ObservableProperty] private string videoPath;

    /// <summary>
    /// 视频的长度（单位：秒）。
    /// </summary>
    [ObservableProperty] private long length;

    /// <summary>
    /// 视频的播放次数。
    /// </summary> 
    private long playCount;

    /// <summary>
    /// 视频的最后修改时间。
    /// </summary>
    [ObservableProperty] [JsonProperty("MidifyTime")]
    private DateTime? modifyTime;

    /// <summary>
    /// 视频评价分数。
    /// </summary> 
    private int evaluate;

    /// <summary>
    /// 视频的快照列表。
    /// </summary>
    public ObservableCollection<Snapshot> Snapshots { get; set; } = new();
}
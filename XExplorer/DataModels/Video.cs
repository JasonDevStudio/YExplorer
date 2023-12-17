using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace XExplorer.DataModels;

/// <summary>
/// s实体基类
/// </summary>
public abstract class ModelBase
{
    [Key]
    public long Id { get; set; }
}

/// <summary>
/// 表示一个视频实体，用于数据库操作。
/// </summary>
[Table("Videos")]
public class Video : ModelBase
{
    /// <summary>
    /// 获取或设置视频的标题。
    /// </summary>
    public string Caption { get; set; }

    /// <summary>
    /// 获取或设置视频文件的存储目录。
    /// </summary>
    public string Dir { get; set; }

    /// <summary>
    /// 获取或设置视频文件的存储目录。
    /// </summary>
    public string VideoDir { get; set; }
    
    /// <summary>
    /// 获取或设置视频文件的快照存储目录。
    /// </summary>
    public string DataDir { get; set; }

    /// <summary>
    /// 获取或设置视频文件的完整路径。
    /// </summary>
    public string VideoPath { get; set; }

    /// <summary>
    /// 获取或设置视频的长度（单位：秒）。
    /// </summary>
    public long Length { get; set; }

    /// <summary>
    /// 获取或设置视频的播放次数。
    /// </summary>
    public long PlayCount { get; set; } = 0;

    /// <summary>
    /// 获取或设置视频的最后修改时间。
    /// </summary>
    public DateTime? ModifyTime { get; set; }

    /// <summary>
    /// 获取或设置视频评价分数。
    /// </summary>
    public int Evaluate { get; set; } = 0;

    /// <summary>
    /// 快照列表.
    /// </summary>
    public List<Snapshot> Snapshots { get; set; } = [];
}

/// <summary>
/// 表示视频快照的实体类。
/// </summary>
[Table("Snapshots")]
public class Snapshot : ModelBase
{
    /// <summary>
    /// 获取或设置与此快照关联的视频的标识符。
    /// </summary>
    [ForeignKey("Video")]
    public long VideoId { get; set; }

    /// <summary>
    /// 获取或设置快照的文件路径。
    /// </summary>
    public string Path { get; set; }
}
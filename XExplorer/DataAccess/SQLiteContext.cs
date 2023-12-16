using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;
using SnowflakeIdGeneratorForCSharp;
using XExplorer.DataModels;

namespace XExplorer.DataAccess;

/// <summary>
/// 代表应用程序的数据库上下文，用于与SQLite数据库交互。
/// </summary>
public class SQLiteContext : DbContext
{
    /// <summary>
    /// 获取或设置数据库文件的路径。
    /// </summary>
    public string DBFile { get; private set; }

    /// <summary>
    /// 雪花ID生成器
    /// </summary>
    public SnowflakeIdGenerator IdGenerator { get; private set; } = new SnowflakeIdGenerator(0, 0);

    #region DBSet

    /// <summary>
    /// 获取或设置视频数据集。
    /// </summary>
    public DbSet<Video> Videos { get; set; }


    /// <summary>
    /// 获取或设置快照集合
    /// </summary>
    public DbSet<Snapshot> Snapshots { get; set; }

    #endregion
    
    /// <summary>
    /// 初始化 SQLiteContext 类的新实例。
    /// </summary>
    /// <param name="dbFile">数据库文件的路径。</param>
    public SQLiteContext(string dbFile)
    {
        DBFile = dbFile;
    }
     
    /// <summary>
    /// 配置数据库（SQLite）的连接字符串。
    /// </summary>
    /// <param name="optionsBuilder">用于构建选项的构建器。</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={DBFile}");
    }

    /// <summary>
    /// 用于配置模型构建的约定。
    /// </summary>
    /// <param name="configurationBuilder">模型配置构建器，用于定义模型构建的约定。</param>
    /// <remarks>
    /// 此方法在模型构建过程中自动被调用，用于配置实体框架核心的默认约定。
    /// 通过这个方法，可以自定义实体的配置，如属性的命名约定、数据类型的映射等。
    /// 这是 EF Core 6.0 中引入的功能，旨在提供更灵活的模型配置方式。
    /// </remarks>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
    }

    /// <summary>
    /// 在构建模型时调用，用于配置实体和数据库之间的映射关系。
    /// </summary>
    /// <param name="modelBuilder">用于构建实体模型的构建器。</param>
    /// <remarks>
    /// 通过重写此方法，可以使用 Fluent API 来配置实体类的数据库映射。
    /// 这包括设置表名称、架构、索引、外键、导航属性等。
    /// 也可以在此方法中设置全局查询过滤器和值转换器等。
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Video>()
            .HasMany(v => v.Snapshots)
            .WithOne()
            .HasForeignKey(s => s.VideoId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>
    /// 根据提供的视频路径创建一个新的视频实体。
    /// </summary>
    /// <param name="videoPath">视频文件的完整路径。</param>
    /// <returns>新创建的视频实体。</returns>
    /// <remarks>
    /// 此方法会生成一个新的视频实体，并为其分配一个唯一的标识符。
    /// 调用此方法时，请确保提供的视频路径是有效的。
    /// </remarks>
    public Video CreateVideo(string videoPath)
    {
        var video = new Video
        {
            VideoPath = videoPath,
            Id = IdGenerator.CreateId(),
        };

        return video;
    }

    /// <summary>
    /// 根据提供的快照路径创建一个新的快照实体。
    /// </summary>
    /// <param name="snapshotPath">快照文件的完整路径。</param>
    /// <param name="videoId">视频ID。</param>
    /// <returns>新创建的快照实体。</returns>
    /// <remarks>
    /// 此方法会生成一个新的快照实体，并为其分配一个唯一的标识符。
    /// 调用此方法时，请确保提供的快照路径是有效的。
    /// </remarks>
    public Snapshot CreateSnapshot(string snapshotPath, long videoId)
    {
        var snapshot = new Snapshot
        {
            Path = snapshotPath,
            Id = IdGenerator.CreateId(),
            VideoId = videoId,
        };

        return snapshot;
    }

}
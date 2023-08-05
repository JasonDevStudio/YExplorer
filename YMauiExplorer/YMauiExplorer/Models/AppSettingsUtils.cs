using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YMauiExplorer.Models;

using System.IO;

/// <summary>
/// 应用程序设置工具类，用于加载和管理应用程序设置。
/// </summary>
public static class AppSettingsUtils
{
    /// <summary>
    /// 获取应用程序的默认设置。
    /// </summary>
    public static AppSettings Default { get; private set; }

    /// <summary>
    /// 从指定的 JSON 文件路径加载应用程序设置。
    /// </summary>
    /// <param name="jsonPath">包含应用程序设置的 JSON 文件的路径。</param>
    public static void LoadJson(string jsonPath)
    {
        var jsonTxt = File.ReadAllText(jsonPath);
        Default = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(jsonTxt);
    }
}



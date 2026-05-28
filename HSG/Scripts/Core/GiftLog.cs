/* 由deepseek编写
   由deepseek编写
   由deepseek编写
   由deepseek编写
   由deepseek编写
   由deepseek编写
   由deepseek编写
   由deepseek编写
   由deepseek编写
   由deepseek编写
   由deepseek编写
   由deepseek编写
   由deepseek编写
   由deepseek编写
 */

using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 日志工具
/// </summary>
public static class HsgDebug
{
    // 日志文件的最终存放路径（全路径）
    private static readonly string LogFilePath;
    // 游戏启动的时间戳，用作日志标题
    private static readonly string StartupTime;

    static HsgDebug()
    {
        StartupTime = DateTime.Now.ToString("yyyy/M/d HH:mm:ss");

        // 1. 优先尝试游戏根目录（Among Us 文件夹）
        string gameRootPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Gift.log");
        string determinedPath = null;

        try
        {
            // 测试根目录是否可写（不破坏已有文件）
            using (File.Open(gameRootPath, FileMode.OpenOrCreate, FileAccess.Write)) { }
            determinedPath = gameRootPath;
        }
        catch
        {
            // 2. 回退到 AppData\LocalLow\Innersloth\Among Us
            string localLow = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "..", "LocalLow", "Innersloth", "Among Us"
            );
            determinedPath = Path.Combine(Path.GetFullPath(localLow), "GiftLog.log");

            // 确保目录存在
            string directory = Path.GetDirectoryName(determinedPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        LogFilePath = determinedPath;

        // 每次启动时，在现有日志末尾追加一条开始信息（不再清空）
        AppendLog("=====Log Started " + StartupTime + "=====");

        // 监听未处理异常，记录崩溃信息
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    /// <summary>
    /// 记录一条普通调试信息。
    /// </summary>
    /// <param name="message">要记录的内容</param>
    public static void Log(string message)
    {
        string logLine = $"Message<{DateTime.Now:yyyy/M/d HH:mm:ss}>: \"{message}\"";
        AppendLog(logLine);
    }

    /// <summary>
    /// 游戏崩溃时自动调用的异常记录器。
    /// </summary>
    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        string exceptionName = (e.ExceptionObject as Exception)?.GetType().Name ?? "UnknownException";
        string logLine = $"Error<{DateTime.Now:HH:mm:ss}>: {exceptionName}";
        AppendLog(logLine);
    }

    /// <summary>
    /// 向日志文件追加一行文本，并自动检查文件大小。
    /// </summary>
    private static void AppendLog(string content)
    {
        try
        {
            File.AppendAllText(LogFilePath, content + Environment.NewLine);
            CheckFileSize();
        }
        catch { /* 静默忽略写入错误 */ }
    }

    /// <summary>
    /// 当日志超过 1MB 时自动清空文件（仅保留重置标记）。
    /// </summary>
    private static void CheckFileSize()
    {
        try
        {
            FileInfo fi = new FileInfo(LogFilePath);
            if (fi.Exists && fi.Length > 1048576)
            {
                File.WriteAllText(LogFilePath,
                    $"=====Log Reset at {DateTime.Now:yyyyMMddHHmmss}====={Environment.NewLine}");
            }
        }
        catch { }
    }

    /// <summary>
    /// 清空日志文件（保留供外部调用，但构造函数中不再使用）。
    /// </summary>
    private static void ClearLog()
    {
        try
        {
            File.WriteAllText(LogFilePath, string.Empty);
        }
        catch { }
    }
}
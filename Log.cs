using System.IO;
using System.Text;

namespace FocusShade;

internal static class Log
{
    private static readonly string LogPath = GetLogPath();
    private static readonly object Lock = new();

    static Log()
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} [INFO] Log initialized. File: {LogPath}{Environment.NewLine}";
            using (var fs = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(fs))
            {
                sw.Write(line);
                sw.Flush();
                fs.Flush(true);
            }
        }
        catch (Exception ex)
        {
            try
            {
                var fallback = Path.Combine(Path.GetTempPath(), "FocusShade_focusshade.log");
                File.AppendAllText(fallback, $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} [ERROR] Log init failed: {ex.Message}. Using fallback: {fallback}{Environment.NewLine}");
            }
            catch { }
        }
    }

    public static string LogFilePath => LogPath;

    /// <summary>Log file next to the exe (or in project output when running from source).</summary>
    private static string GetLogPath()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir))
                return Path.Combine(baseDir, "focusshade.log");
        }
        catch { }
        return Path.Combine(Path.GetTempPath(), "FocusShade_focusshade.log");
    }

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(string message, Exception? ex = null)
    {
        var sb = new StringBuilder(message);
        if (ex != null)
        {
            sb.AppendLine();
            sb.Append(ex);
        }
        Write("ERROR", sb.ToString());
    }

    private static void Write(string level, string message)
    {
        lock (Lock)
        {
            try
            {
                var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
                using (var fs = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var sw = new StreamWriter(fs))
                {
                    sw.WriteLine(line);
                    sw.Flush();
                    fs.Flush(true);
                }
            }
            catch
            {
                try
                {
                    var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
                    File.AppendAllText(Path.Combine(Path.GetTempPath(), "focusshade_fallback.log"), line + Environment.NewLine);
                }
                catch { }
            }
        }
    }
}

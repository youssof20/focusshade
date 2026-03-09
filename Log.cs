using System.IO;
using System.Text;

namespace FocusShade;

internal static class Log
{
    private static readonly string LogPath = GetLogPath();
    private static readonly object Lock = new();

    public static string LogFilePath => LogPath;

    private static string GetLogPath()
    {
        try
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FocusShade");
            Directory.CreateDirectory(appData);
            return Path.Combine(appData, "focusshade.log");
        }
        catch
        {
            try
            {
                var exeDir = AppContext.BaseDirectory;
                return Path.Combine(exeDir, "focusshade.log");
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "focusshade.log");
            }
        }
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
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch
            {
                // ignore
            }
        }
    }
}

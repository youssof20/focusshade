using System.IO;

namespace FocusShade;

/// <summary>
/// Writes a minidump on unhandled (managed) exceptions so you can open it in Visual Studio or WinDbg.
/// For native crashes, use Procdump (see DEBUGGING.md).
/// </summary>
internal static class CrashDump
{
    private const uint MiniDumpWithDataSegs = 1; // smaller; use MiniDumpWithFullMemory = 2 for full memory
    private static readonly IntPtr InvalidHandle = new(-1);

    /// <summary>
    /// Writes a minidump of the current process to %LOCALAPPDATA%\FocusShade\CrashDumps\.
    /// Returns the full path of the .dmp file, or null if writing failed.
    /// </summary>
    public static string? WriteMinidump()
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusShade", "CrashDumps");
            Directory.CreateDirectory(dir);
            string name = $"FocusShade_{DateTime.UtcNow:yyyyMMdd_HHmmss}.dmp";
            string path = Path.Combine(dir, name);

            IntPtr hFile = NativeMethods.CreateFile(
                path,
                NativeMethods.GENERIC_WRITE,
                NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.CREATE_ALWAYS,
                0,
                IntPtr.Zero);

            if (hFile == InvalidHandle || hFile == IntPtr.Zero)
                return null;

            try
            {
                if (!NativeMethods.MiniDumpWriteDump(
                    NativeMethods.GetCurrentProcess(),
                    (uint)Environment.ProcessId,
                    hFile,
                    MiniDumpWithDataSegs,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero))
                    return null;
            }
            finally
            {
                NativeMethods.CloseHandle(hFile);
            }

            return path;
        }
        catch
        {
            return null;
        }
    }
}

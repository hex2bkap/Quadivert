namespace PixShift.Services;

public class LogService
{
    private static readonly string LogDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixShift", "logs");

    private readonly string _logFile;
    private readonly object _lock = new();

    public LogService()
    {
        _logFile = Path.Combine(LogDir, $"app-{DateTime.Now:yyyy-MM-dd}.log");
        CleanOldLogs(keepDays: 30);
    }

    private static void CleanOldLogs(int keepDays)
    {
        try
        {
            if (!Directory.Exists(LogDir)) return;
            var cutoff = DateTime.Today.AddDays(-keepDays);
            foreach (var file in Directory.EnumerateFiles(LogDir, "app-*.log"))
            {
                var stem = Path.GetFileNameWithoutExtension(file);
                if (stem.Length >= 14 && DateTime.TryParse(stem[4..], out var fileDate) && fileDate < cutoff)
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        catch { }
    }

    public void Info(string message) => Write("INFO ", message);
    public void Warn(string message) => Write("WARN ", message);

    public void Error(Exception ex, string context = "")
    {
        var msg = string.IsNullOrEmpty(context)
            ? $"{ex.GetType().Name}: {ex.Message}"
            : $"[{context}] {ex.GetType().Name}: {ex.Message}";
        Write("ERROR", msg);
        if (ex.StackTrace != null)
            Write("TRACE", ex.StackTrace);
    }

    private void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            lock (_lock) { File.AppendAllText(_logFile, line); }
        }
        catch { }
    }
}

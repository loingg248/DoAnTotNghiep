using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using SystemMonitor.Services;

public class SystemUsageLogger
{
    private readonly string _logFile;

    public SystemUsageLogger(string logFilePath = "Logs/system_usage.csv")
    {
        _logFile = logFilePath;

        // Tạo thư mục nếu chưa có
        Directory.CreateDirectory(Path.GetDirectoryName(_logFile));

        // Nếu file chưa tồn tại, ghi header
        if (!File.Exists(_logFile))
        {
            using (var writer = new StreamWriter(_logFile, append: false))
            {
                writer.WriteLine("Timestamp,CpuUsage,GpuUsage,RamUsage,Label");
            }
        }
    }

    public void Log(float cpuUsage, float gpuUsage, float ramUsage, string label)
    {
        try
        {
            using (var writer = new StreamWriter(_logFile, append: true))
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1:F1},{2:F1},{3:F1},{4}",
                    timestamp, cpuUsage, gpuUsage, ramUsage, label));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lỗi ghi log: {ex.Message}");
        }
    }

    public void Log(SystemInfoEventArgs systemInfo, string label)
    {
        try
        {
            bool fileExists = File.Exists(_logFile);
            using (var writer = new StreamWriter(_logFile, append: true))
            {
                if (!fileExists)
                {
                    writer.WriteLine("Timestamp,CpuUsage,GpuUsage,RamUsage,Label");
                }

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1:F1},{2:F1},{3:F1},{4}",
                    timestamp,
                    systemInfo.CpuUsage,
                    systemInfo.GpuUsage,
                    systemInfo.RamUsage,
                    label));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lỗi ghi log: {ex.Message}");
        }
    }
}

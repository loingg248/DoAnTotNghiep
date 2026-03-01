using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SystemMonitor.Services
{
    public class WarningService
    {
        // Thêm event cho background mode
        public event EventHandler<string> WarningTriggered;
        public event Action<List<SolutionRecommendation>> SolutionsUpdated;
        public event Action<bool> SolutionsVisibilityChanged;

        // Existing properties...
        public float CpuWarningThreshold { get; set; } = 85f;
        public float CpuTemperatureThreshold { get; set; } = 95f;
        public float RamWarningThreshold { get; set; } = 90f;
        public float GpuWarningThreshold { get; set; } = 85f;
        public float GpuTemperatureThreshold { get; set; } = 85f;
        public float DiskWarningThreshold { get; set; } = 95f;

        // UI Controls
        private TextBlock _warningStatus;
        private Border _warningBorder;
        private TextBlock _warningTitle;
        private TextBlock _warningIcon;
        private ProgressBar _warningProgress;

        // Background mode
        public bool IsBackgroundMode { get; private set; } = false;

        // Warning status
        private bool _isWarningActive = false;
        private DateTime _lastWarningTime = DateTime.MinValue;
        private readonly TimeSpan _warningCooldown = TimeSpan.FromSeconds(60);
        private DateTime _initTime = DateTime.Now;

        // History và tracking để đưa ra giải pháp "thông minh"
        private readonly TimeSpan _persistenceWindow = TimeSpan.FromMinutes(5);
        private readonly List<(DateTime Time, string MetricKey)> _warningHistory = new();
        private readonly Dictionary<string, DateTime> _solutionLastRecommended = new();
        private readonly TimeSpan _solutionRepeatCooldown = TimeSpan.FromSeconds(120);

        public void SetUIControls(TextBlock warningStatus, Border warningBorder)
        {
            _warningStatus = warningStatus;
            _warningBorder = warningBorder;
        }

        public void SetBackgroundMode(bool isBackground)
        {
            IsBackgroundMode = isBackground;
        }

        public record SolutionRecommendation
        {
            public string Icon { get; init; }
            public string Title { get; init; }
            public string Description { get; init; }
            public string Action { get; init; }
            public string ActionType { get; init; }
            public string ActionData { get; init; }
        }

        // Thêm method mới để tạo giải pháp cụ thể
        private List<SolutionRecommendation> GetSpecificSolutions(List<string> warnings)
        {
            var solutions = new List<SolutionRecommendation>();

            foreach (var warning in warnings)
            {
                if (warning.Contains("CPU:"))
                {
                    solutions.AddRange(GetCPUSolutions());
                }
                else if (warning.Contains("CPU Temp:"))
                {
                    solutions.AddRange(GetCPUTempSolutions());
                }
                else if (warning.Contains("RAM:"))
                {
                    solutions.AddRange(GetRAMSolutions());
                }
                else if (warning.Contains("GPU:"))
                {
                    solutions.AddRange(GetGPUSolutions());
                }
                else if (warning.Contains("GPU Temp:"))
                {
                    solutions.AddRange(GetGPUTempSolutions());
                }
                else if (warning.Contains("Disk:"))
                {
                    solutions.AddRange(GetDiskSolutions());
                }
            }

            // record hỗ trợ equality theo giá trị nên Distinct() sẽ hoạt động
            return solutions.Distinct().ToList();
        }

        // New: Smart ranking + de-dup + persistence-aware solutions
        private List<SolutionRecommendation> GetSmartSolutions(List<string> warnings, SystemInfoEventArgs systemInfo)
        {
            // Update history (persistence) based on current warnings
            var now = DateTime.Now;
            foreach (var w in warnings)
            {
                var key = GetMetricKeyFromWarning(w);
                _warningHistory.Add((now, key));
            }

            // Prune old history
            _warningHistory.RemoveAll(h => now - h.Time > _persistenceWindow);

            // Candidate solutions (distinct)
            var candidates = GetSpecificSolutions(warnings);

            // Compute per-metric persistence factor (0.5..2.0)
            var metricCounts = _warningHistory
                .GroupBy(h => h.MetricKey)
                .ToDictionary(g => g.Key, g => g.Count());

            // Score each candidate based on severity & persistence & last recommended cooldown
            var scored = new List<(SolutionRecommendation sol, double score)>();

            foreach (var candidate in candidates)
            {
                double baseScore = 1.0;

                // If candidate.ActionData references a metric, boost it. Use Title/ActionData
                var key = (candidate.ActionData ?? candidate.Title ?? string.Empty).ToUpperInvariant();

                // Severity: aggregate warnings severity (0..1)
                double severity = 0.0;
                foreach (var w in warnings)
                {
                    severity = Math.Max(severity, CalculateSeverityForWarning(w, systemInfo));
                }
                double severityFactor = 1.0 + severity * 2.0; // 1.0..3.0

                // Persistence factor: if the metric has repeated occurrences then boost
                double persistenceFactor = 1.0;
                foreach (var w in warnings)
                {
                    var metricKey = GetMetricKeyFromWarning(w);
                    if (metricCounts.TryGetValue(metricKey, out var cnt))
                    {
                        persistenceFactor = Math.Max(persistenceFactor, 1.0 + Math.Min(2.0, cnt / 3.0)); // up to x3
                    }
                }

                // Penalize if we recently recommended same solution
                double repeatPenalty = 1.0;
                var solutionUniqueKey = GetSolutionUniqueKey(candidate);
                if (_solutionLastRecommended.TryGetValue(solutionUniqueKey, out var lastRec))
                {
                    var elapsed = now - lastRec;
                    if (elapsed < _solutionRepeatCooldown)
                    {
                        repeatPenalty = 0.3; // heavily deprioritize
                    }
                    else
                    {
                        repeatPenalty = 1.0 + Math.Min(0.5, elapsed.TotalSeconds / _solutionRepeatCooldown.TotalSeconds);
                    }
                }

                double score = baseScore * severityFactor * persistenceFactor * repeatPenalty;
                scored.Add((candidate, score));
            }

            // Return top solutions ordered by score, distinct by Title+ActionData
            var top = scored
                .OrderByDescending(s => s.score)
                .Select(s => s.sol)
                .GroupBy(s => GetSolutionUniqueKey(s))
                .Select(g => g.First())
                .ToList();

            // Update last recommended times for chosen top few (so won't repeat too often)
            foreach (var sol in top.Take(6))
            {
                _solutionLastRecommended[GetSolutionUniqueKey(sol)] = now;
            }

            return top;
        }

        private string GetSolutionUniqueKey(SolutionRecommendation s)
        {
            return $"{(s.Title ?? "")}|{(s.ActionData ?? "")}".ToUpperInvariant();
        }

        private string GetMetricKeyFromWarning(string warning)
        {
            if (warning.Contains("CPU Temp:", StringComparison.OrdinalIgnoreCase)) return "CPU_TEMP";
            if (warning.Contains("CPU:", StringComparison.OrdinalIgnoreCase)) return "CPU";
            if (warning.Contains("RAM:", StringComparison.OrdinalIgnoreCase)) return "RAM";
            if (warning.Contains("GPU Temp:", StringComparison.OrdinalIgnoreCase)) return "GPU_TEMP";
            if (warning.Contains("GPU:", StringComparison.OrdinalIgnoreCase)) return "GPU";
            if (warning.Contains("Disk:", StringComparison.OrdinalIgnoreCase)) return "DISK";
            return "UNKNOWN";
        }

        private double CalculateSeverityForWarning(string warning, SystemInfoEventArgs info)
        {
            // Normalize severity between 0 and 1 based on how far beyond threshold the metric is.
            try
            {
                if (warning.StartsWith("CPU Temp:", StringComparison.OrdinalIgnoreCase))
                {
                    double diff = info.CpuTemperature - CpuTemperatureThreshold;
                    return Math.Clamp(diff / Math.Max(1, 120 - CpuTemperatureThreshold), 0, 1);
                }
                if (warning.StartsWith("CPU:", StringComparison.OrdinalIgnoreCase))
                {
                    double diff = info.CpuUsage - CpuWarningThreshold;
                    return Math.Clamp(diff / Math.Max(1, 100 - CpuWarningThreshold), 0, 1);
                }
                if (warning.StartsWith("RAM:", StringComparison.OrdinalIgnoreCase))
                {
                    double diff = info.RamUsage - RamWarningThreshold;
                    return Math.Clamp(diff / Math.Max(1, 100 - RamWarningThreshold), 0, 1);
                }
                if (warning.StartsWith("GPU Temp:", StringComparison.OrdinalIgnoreCase))
                {
                    double diff = info.GpuTemperature - GpuTemperatureThreshold;
                    return Math.Clamp(diff / Math.Max(1, 120 - GpuTemperatureThreshold), 0, 1);
                }
                if (warning.StartsWith("GPU:", StringComparison.OrdinalIgnoreCase))
                {
                    double diff = info.GpuUsage - GpuWarningThreshold;
                    return Math.Clamp(diff / Math.Max(1, 100 - GpuWarningThreshold), 0, 1);
                }
                if (warning.StartsWith("Disk:", StringComparison.OrdinalIgnoreCase))
                {
                    double diff = info.DiskUsage - DiskWarningThreshold;
                    return Math.Clamp(diff / Math.Max(1, 100 - DiskWarningThreshold), 0, 1);
                }
            }
            catch { }

            return 0.0;
        }

        private List<SolutionRecommendation> GetCPUSolutions()
        {
            return new List<SolutionRecommendation>
            {
                new() {
                    Icon = "⚡",
                    Title = "Chuyển Power Plan",
                    Description = "Chuyển sang chế độ tiết kiệm năng lượng",
                    Action = "Vào tab Power Settings → Chọn Power Saver hoặc Balanced",
                    ActionType = "TAB_SWITCH",
                    ActionData = "PowerSettings|PowerSaver"
                },
                new() {
                    Icon = "🔧",
                    Title = "Giảm CPU Frequency",
                    Description = "Hạ tốc độ CPU để giảm tải",
                    Action = "Tab Power Settings → Giảm Max CPU Frequency xuống 70-80%",
                    ActionType = "TAB_SWITCH",
                    ActionData = "PowerSettings|SetMaxCPU|75"
                },
                new() {
                    Icon = "🤖",
                    Title = "Bật Smart Mode",
                    Description = "Để hệ thống tự động điều chỉnh",
                    Action = "Tab Power Settings → Enable Smart Mode",
                    ActionType = "TAB_SWITCH",
                    ActionData = "PowerSettings|EnableSmartMode"
                },
                new() {
                    Icon = "🔄",
                    Title = "Quản lý Process",
                    Description = "Kiểm tra và đóng process tốn CPU",
                    Action = "Tab Process Monitor → Tìm process CPU cao → End Process",
                    ActionType = "TAB_SWITCH",
                    ActionData = "ProcessMonitor|SortByCPU"
                },
                new() {
                    Icon = "🛠️",
                    Title = "Tắt Startup Programs",
                    Description = "Vô hiệu hóa các chương trình khởi động không cần thiết",
                    Action = "Task Manager → Startup → Disable unnecessary programs",
                    ActionType = "OPEN_TASK_MANAGER",
                    ActionData = "Startup"
                },
                new() {
                    Icon = "🔍",
                    Title = "Kiểm tra Malware",
                    Description = "Quét hệ thống để tìm phần mềm độc hại",
                    Action = "Chạy Windows Defender → Full Scan",
                    ActionType = "RUN_DEFENDER",
                    ActionData = "FullScan"
                }
            };
        }

        private List<SolutionRecommendation> GetCPUTempSolutions()
        {
            return new List<SolutionRecommendation>
            {
                new() {
                    Icon = "❄️",
                    Title = "Giảm CPU Performance",
                    Description = "Hạ Max CPU Frequency để giảm nhiệt",
                    Action = "Tab Power Settings → Set Max CPU Frequency = 60-70%",
                    ActionType = "TAB_SWITCH",
                    ActionData = "PowerSettings|SetMaxCPU|65"
                },
                new() {
                    Icon = "🔋",
                    Title = "Power Saver Mode",
                    Description = "Chuyển sang chế độ tiết kiệm năng lượng",
                    Action = "Tab Power Settings → Chọn Power Saver Plan",
                    ActionType = "PowerPlan",
                    ActionData = "PowerSaver"
                },
                new() {
                    Icon = "🌡️",
                    Title = "Kiểm tra tản nhiệt",
                    Description = "Vệ sinh quạt CPU và tản nhiệt",
                    Action = "Tắt máy → Vệ sinh quạt → Thay keo tản nhiệt",
                    ActionType = "HARDWARE_CHECK",
                    ActionData = "CPU_COOLING"
                },
                new() {
                    Icon = "💨",
                    Title = "Tăng luồng gió",
                    Description = "Đảm bảo luồng không khí trong case tốt",
                    Action = "Kiểm tra vị trí case → Đảm bảo không bị chặn luồng gió",
                    ActionType = "HARDWARE_CHECK",
                    ActionData = "CASE_AIRFLOW"
                },
                new() {
                    Icon = "🖥️",
                    Title = "Giảm tải ứng dụng",
                    Description = "Đóng các ứng dụng nặng để giảm nhiệt CPU",
                    Action = "Task Manager → Processes → End heavy applications",
                    ActionType = "TAB_SWITCH",
                    ActionData = "ProcessMonitor|SortByCPU"
                }
            };
        }

        private List<SolutionRecommendation> GetRAMSolutions()
        {
            return new List<SolutionRecommendation>
            {
                new() {
                    Icon = "🔄",
                    Title = "Tìm Process tốn RAM",
                    Description = "Kiểm tra process sử dụng RAM cao",
                    Action = "Tab Process Monitor → Sắp xếp theo Memory → End process không cần thiết",
                    ActionType = "TAB_SWITCH",
                    ActionData = "ProcessMonitor|SortByMemory"
                },
                new() {
                    Icon = "⚠️",
                    Title = "Giới hạn tài nguyên",
                    Description = "Đặt giới hạn RAM cho process",
                    Action = "Process Monitor → Right click process → Set Resource Limit",
                    ActionType = "LIMIT_RESOURCE",
                    ActionData = "Memory"
                },
                new() {
                    Icon = "🗑️",
                    Title = "Dọn dẹp hệ thống",
                    Description = "Giải phóng bộ nhớ không sử dụng",
                    Action = "Chạy Disk Cleanup → Clear Temp files → Restart browser",
                    ActionType = "MAINTENANCE",
                    ActionData = "DiskCleanup"
                },
                new() {
                    Icon = "🛠️",
                    Title = "Tăng Virtual Memory",
                    Description = "Tăng dung lượng bộ nhớ ảo",
                    Action = "Control Panel → System → Advanced → Performance Settings → Adjust Virtual Memory",
                    ActionType = "SYSTEM_SETTING",
                    ActionData = "VirtualMemory"
                },
                new() {
                    Icon = "🔍",
                    Title = "Kiểm tra Memory Leaks",
                    Description = "Kiểm tra ứng dụng gây rò rỉ bộ nhớ",
                    Action = "Task Manager → Processes → Monitor memory usage over time",
                    ActionType = "DIAGNOSTIC",
                    ActionData = "MemoryLeakCheck"
                }
            };
        }

        private List<SolutionRecommendation> GetGPUSolutions()
        {
            return new List<SolutionRecommendation>
            {
                new() {
                    Icon = "🚀",
                    Title = "Chuyển Power Plan GPU",
                    Description = "Chuyển sang Balanced hoặc Power Saver để giảm load GPU",
                    Action = "Tab Power Settings → Chọn Power Plan phù hợp",
                    ActionType = "PowerPlan",
                    ActionData = "Balanced"
                },
                new() {
                    Icon = "🔄",
                    Title = "Kiểm tra Process GPU",
                    Description = "Tìm ứng dụng đang sử dụng GPU cao",
                    Action = "Tab Process Monitor → Tìm process graphics-intensive → Đóng nếu không cần",
                    ActionType = "TAB_SWITCH",
                    ActionData = "ProcessMonitor|SortByGPU"
                },
                new() {
                    Icon = "⚡",
                    Title = "Giảm hiệu năng ứng dụng đồ họa",
                    Description = "Hạ settings đồ họa trong game/app",
                    Action = "Giảm resolution, texture quality, disable effects",
                    ActionType = "USER_ACTION",
                    ActionData = "ReduceGraphicsSettings"
                },
                new() {
                    Icon = "🔧",
                    Title = "Cập nhật Driver GPU",
                    Description = "Cài đặt phiên bản driver GPU mới nhất",
                    Action = "Device Manager → Display Adapters → Update Driver",
                    ActionType = "DRIVER_UPDATE",
                    ActionData = "GPU"
                },
                new() {
                    Icon = "🎮",
                    Title = "Tối ưu Game Settings",
                    Description = "Sử dụng chế độ hiệu năng thấp trong game",
                    Action = "Game Settings → Select Low Performance Mode",
                    ActionType = "USER_ACTION",
                    ActionData = "GameLowPerf"
                }
            };
        }

        private List<SolutionRecommendation> GetGPUTempSolutions()
        {
            return new List<SolutionRecommendation>
            {
                new() {
                    Icon = "🔋",
                    Title = "Power Management GPU",
                    Description = "Giảm power limit của GPU",
                    Action = "Tab Power Settings → Chọn Power Saver để giảm GPU load",
                    ActionType = "PowerPlan",
                    ActionData = "PowerSaverGPU"
                },
                new() {
                    Icon = "🌪️",
                    Title = "Tăng tốc quạt GPU",
                    Description = "Tăng fan curve GPU",
                    Action = "MSI Afterburner → Tăng fan speed → Custom fan curve",
                    ActionType = "HARDWARE_TUNE",
                    ActionData = "GPU_FAN"
                },
                new() {
                    Icon = "❄️",
                    Title = "Vệ sinh làm mát GPU",
                    Description = "Thổi bụi GPU và case",
                    Action = "Tắt máy → Compressed air → Vệ sinh GPU cooler",
                    ActionType = "HARDWARE_CHECK",
                    ActionData = "GPU_COOLING"
                },
                new() {
                    Icon = "🌡️",
                    Title = "Theo dõi Nhiệt độ GPU",
                    Description = "Theo dõi nhiệt độ GPU trong thời gian thực",
                    Action = "Sử dụng GPU-Z hoặc MSI Afterburner để kiểm tra",
                    ActionType = "DIAGNOSTIC",
                    ActionData = "GPU_MONITOR"
                },
                new() {
                    Icon = "🛠️",
                    Title = "Undervolt GPU",
                    Description = "Giảm điện áp GPU để hạ nhiệt độ",
                    Action = "MSI Afterburner → Adjust voltage → Apply stable undervolt",
                    ActionType = "HARDWARE_TUNE",
                    ActionData = "GPU_UNDERVOLT"
                }
            };
        }

        private List<SolutionRecommendation> GetDiskSolutions()
        {
            return new List<SolutionRecommendation>
            {
                new() {
                    Icon = "🔄",
                    Title = "Kiểm tra Disk Activity",
                    Description = "Tìm process đang sử dụng disk cao",
                    Action = "Tab Process Monitor → Tìm process I/O intensive → Tạm dừng hoặc đóng",
                    ActionType = "TAB_SWITCH",
                    ActionData = "ProcessMonitor|SortByIO"
                },
                new() {
                    Icon = "⚠️",
                    Title = "Giới hạn I/O",
                    Description = "Đặt giới hạn disk usage cho process",
                    Action = "Process Monitor → Right click → Set Resource Limit cho disk usage",
                    ActionType = "LIMIT_RESOURCE",
                    ActionData = "IO"
                },
                new() {
                    Icon = "⏸️",
                    Title = "Tạm dừng backup/scan",
                    Description = "Dừng antivirus hoặc backup đang chạy",
                    Action = "Tạm dừng Windows Defender scan, OneDrive sync, backup software",
                    ActionType = "USER_ACTION",
                    ActionData = "PauseBackupScan"
                },
                new() {
                    Icon = "💾",
                    Title = "Disk Sleep Settings",
                    Description = "Tối ưu disk power management",
                    Action = "Tab Power Settings → Điều chỉnh sleep settings cho disk",
                    ActionType = "PowerPlan",
                    ActionData = "DiskSleep"
                },
                new() {
                    Icon = "🗑️",
                    Title = "Dọn dẹp ổ đĩa",
                    Description = "Xóa file rác và tối ưu hóa ổ đĩa",
                    Action = "Disk Cleanup → Defragment and Optimize Drives",
                    ActionType = "MAINTENANCE",
                    ActionData = "DiskCleanup"
                },
                new() {
                    Icon = "🔍",
                    Title = "Kiểm tra sức khỏe ổ đĩa",
                    Description = "Kiểm tra lỗi và tình trạng ổ đĩa",
                    Action = "Chạy Check Disk → Scan for and fix errors",
                    ActionType = "DIAGNOSTIC",
                    ActionData = "CheckDisk"
                }
            };
        }

        private List<SolutionRecommendation> GetCPUSolutionsWithActions()
        {
            return new List<SolutionRecommendation>
            {
                new() {
                    Icon = "🤖",
                    Title = "Bật Smart Mode",
                    Description = "Tự động điều chỉnh CPU theo tải",
                    Action = "Nhấn Enable Smart Mode trong tab Power Settings",
                    ActionType = "TAB_SWITCH",
                    ActionData = "PowerSettings|EnableSmartMode"
                },
                new() {
                    Icon = "🔧",
                    Title = "Giảm Max CPU Frequency",
                    Description = "Hạ tốc độ CPU xuống 70%",
                    Action = "Set Max CPU Frequency = 70% trong Power Settings",
                    ActionType = "TAB_SWITCH",
                    ActionData = "PowerSettings|SetMaxCPU|70"
                },
                new() {
                    Icon = "🔄",
                    Title = "Đóng Process tốn CPU",
                    Description = "Tìm và đóng process CPU cao nhất",
                    Action = "Chuyển sang Process Monitor để xem chi tiết",
                    ActionType = "TAB_SWITCH",
                    ActionData = "ProcessMonitor|SortByCPU"
                },
                new() {
                    Icon = "🛠️",
                    Title = "Tắt Startup Programs",
                    Description = "Vô hiệu hóa các chương trình khởi động không cần thiết",
                    Action = "Task Manager → Startup → Disable unnecessary programs",
                    ActionType = "OPEN_TASK_MANAGER",
                    ActionData = "Startup"
                },
                new() {
                    Icon = "🔍",
                    Title = "Kiểm tra Malware",
                    Description = "Quét hệ thống để tìm phần mềm độc hại",
                    Action = "Chạy Windows Defender → Full Scan",
                    ActionType = "RUN_DEFENDER",
                    ActionData = "FullScan"
                }
            };
        }

        public void CheckSystemOverload(SystemInfoEventArgs systemInfo)
        {
            var warnings = new List<string>();
            bool isOverloaded = false;

            // DEBUG: In ra thông tin hiện tại
            System.Diagnostics.Debug.WriteLine("=== DEBUG WARNING SYSTEM ===");
            System.Diagnostics.Debug.WriteLine($"CPU: {systemInfo.CpuUsage:F1}% (Threshold: {CpuWarningThreshold}%)");
            System.Diagnostics.Debug.WriteLine($"GPU: {systemInfo.GpuUsage:F1}% (Threshold: {GpuWarningThreshold}%)");
            System.Diagnostics.Debug.WriteLine($"GPU Temp: {systemInfo.GpuTemperature:F1}°C (Threshold: {GpuTemperatureThreshold}°C)");
            System.Diagnostics.Debug.WriteLine($"RAM: {systemInfo.RamUsage:F1}% (Threshold: {RamWarningThreshold}%)");
            System.Diagnostics.Debug.WriteLine($"Disk: {systemInfo.DiskUsage:F1}% (Threshold: {DiskWarningThreshold}%)");
            System.Diagnostics.Debug.WriteLine($"Last warning: {DateTime.Now - _lastWarningTime} ago");
            System.Diagnostics.Debug.WriteLine($"Should show warning: {ShouldShowWarning()}");
            System.Diagnostics.Debug.WriteLine($"Background mode: {IsBackgroundMode}");

            // Chờ 5 giây sau khi khởi tạo
            if (DateTime.Now - _initTime < TimeSpan.FromSeconds(5))
            {
                System.Diagnostics.Debug.WriteLine("⏳ Đang trong thời gian chờ 5 giây khởi tạo");
                return;
            }

            // Kiểm tra ngưỡng truyền thống
            if (systemInfo.CpuUsage > CpuWarningThreshold)
            {
                warnings.Add($"CPU: {systemInfo.CpuUsage:F1}%");
                isOverloaded = true;
            }
            if (systemInfo.CpuTemperature > CpuTemperatureThreshold)
            {
                warnings.Add($"CPU Temp: {systemInfo.CpuTemperature:F1}°C");
                isOverloaded = true;
            }
            if (systemInfo.RamUsage > RamWarningThreshold)
            {
                warnings.Add($"RAM: {systemInfo.RamUsage:F1}%");
                isOverloaded = true;
            }
            if (systemInfo.GpuUsage > GpuWarningThreshold)
            {
                warnings.Add($"GPU: {systemInfo.GpuUsage:F1}%");
                isOverloaded = true;
            }
            if (systemInfo.GpuTemperature > GpuTemperatureThreshold)
            {
                warnings.Add($"GPU Temp: {systemInfo.GpuTemperature:F1}°C");
                isOverloaded = true;
            }
            if (systemInfo.DiskUsage > DiskWarningThreshold)
            {
                warnings.Add($"Disk: {systemInfo.DiskUsage:F1}%");
                isOverloaded = true;
            }

            System.Diagnostics.Debug.WriteLine($"Tổng cảnh báo: {warnings.Count}, isOverloaded: {isOverloaded}");

            // Xử lý cảnh báo
            if (isOverloaded && ShouldShowWarning())
            {
                _lastWarningTime = DateTime.Now; // cập nhật thời gian cảnh báo

                var smartSolutions = GetSmartSolutions(warnings, systemInfo);

                if (IsBackgroundMode)
                {
                    string warningText = string.Join(", ", warnings);
                    WarningTriggered?.Invoke(this, warningText);
                    SolutionsUpdated?.Invoke(smartSolutions);
                    SolutionsVisibilityChanged?.Invoke(false);
                }
                else
                {
                    App.Current?.Dispatcher.Invoke(() =>
                    {
                        UpdateWarningUI(isOverloaded, warnings);
                    });
                    SolutionsUpdated?.Invoke(smartSolutions);
                    SolutionsVisibilityChanged?.Invoke(true);
                    ShowWarningPopupWithSolutions(warnings, smartSolutions);
                }
            }
            else if (!IsBackgroundMode)
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    UpdateWarningUI(isOverloaded, warnings);
                });

                if (!isOverloaded)
                {
                    System.Diagnostics.Debug.WriteLine("✅ Hệ thống hoạt động bình thường");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⏳ Có cảnh báo nhưng đang trong cooldown period");
                }
            }

            System.Diagnostics.Debug.WriteLine("================================");
        }


        // Existing methods remain the same...
        private void UpdateWarningUI(bool isOverloaded, List<string> warnings)
        {
            if (_warningStatus == null || _warningBorder == null) return;

            if (isOverloaded)
            {
                _isWarningActive = true;
                string warningText = "⚠️ Issues detected: " + string.Join(" | ", warnings);
                _warningStatus.Text = warningText;

                _warningBorder.Background = new SolidColorBrush(Color.FromRgb(255, 243, 205));
                _warningBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7));
                _warningBorder.BorderThickness = new Thickness(2);
                _warningBorder.Visibility = Visibility.Visible;

                // Trigger event thay vì truy cập trực tiếp MainWindow
                var solutions = GetSpecificSolutions(warnings);
                SolutionsUpdated?.Invoke(solutions);
                SolutionsVisibilityChanged?.Invoke(true);
            }
            else
            {
                if (_isWarningActive)
                {
                    _isWarningActive = false;
                    _warningStatus.Text = "✅ System operating normally";
                    _warningBorder.Background = new SolidColorBrush(Color.FromRgb(212, 237, 218));
                    _warningBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(25, 135, 84));

                    // Trigger event để ẩn solutions
                    SolutionsVisibilityChanged?.Invoke(false);
                }
                else
                {
                    _warningBorder.Visibility = Visibility.Collapsed;
                }
            }
        }

        private bool ShouldShowWarning()
        {
            return DateTime.Now - _lastWarningTime > _warningCooldown;
        }

        private void ShowWarningPopupWithSolutions(List<string> warnings, List<SolutionRecommendation> solutions)
        {
            _lastWarningTime = DateTime.Now;

            string message = "⚠️ HỆ THỐNG CẢNH BÁO!\n\n";
            message += "🔍 VẤN ĐỀ PHÁT HIỆN:\n";

            foreach (var warning in warnings)
            {
                message += $"• {warning}\n";
            }

            message += "\n💡 GIẢI PHÁP ĐỀ XUẤT (ưu tiên):\n";

            foreach (var solution in solutions.Take(6)) // Giới hạn 6 giải pháp
            {
                message += $"{solution.Icon} {solution.Title}: {solution.Description}\n";
                message += $"   → {solution.Action}\n\n";
            }

            message += "⏰ Cảnh báo sẽ lặp lại sau 60 giây nếu vẫn có vấn đề.";

            MessageBox.Show(message, "System Warning - Smart Solutions",
                           MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void UpdateThresholds(float cpuThreshold, float cpuTempThreshold, float ramThreshold,
                                    float gpuThreshold, float gpuTempThreshold, float diskThreshold)
        {
            CpuWarningThreshold = Math.Max(1, Math.Min(100, cpuThreshold));     // Cho phép từ 1% đến 100%
            CpuTemperatureThreshold = Math.Max(30, Math.Min(100, cpuTempThreshold)); // Nhiệt độ từ 30°C
            RamWarningThreshold = Math.Max(1, Math.Min(100, ramThreshold));     // RAM từ 1%
            GpuWarningThreshold = Math.Max(1, Math.Min(100, gpuThreshold));     // GPU từ 1%
            GpuTemperatureThreshold = Math.Max(30, Math.Min(100, gpuTempThreshold)); // GPU temp từ 30°C
            DiskWarningThreshold = Math.Max(1, Math.Min(100, diskThreshold));   // Disk từ 1%
        }
    }
}
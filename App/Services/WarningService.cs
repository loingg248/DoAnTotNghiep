using System;
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
        public float CpuTemperatureThreshold { get; set; } = 90f;
        public float RamWarningThreshold { get; set; } = 90f;
        public float GpuWarningThreshold { get; set; } = 85f;
        public float GpuTemperatureThreshold { get; set; } = 85f;
        public float DiskWarningThreshold { get; set; } = 95f;

        // UI Controls
        private TextBlock _warningStatus;
        private Border _warningBorder;

        // Background mode
        public bool IsBackgroundMode { get; private set; } = false;

        // Warning status
        private bool _isWarningActive = false;
        private DateTime _lastWarningTime = DateTime.MinValue;
        private readonly TimeSpan _warningCooldown = TimeSpan.FromSeconds(60);
        private DateTime _initTime = DateTime.Now;

        public void SetUIControls(TextBlock warningStatus, Border warningBorder)
        {
            _warningStatus = warningStatus;
            _warningBorder = warningBorder;
        }

        public void SetBackgroundMode(bool isBackground)
        {
            IsBackgroundMode = isBackground;
        }

        public class SolutionRecommendation
        {
            public string Icon { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string Action { get; set; }
            public string ActionType { get; set; } 
            public string ActionData { get; set; }
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

            return solutions.Distinct().ToList();
        }

        private List<SolutionRecommendation> GetCPUSolutions()
        {
            return new List<SolutionRecommendation>
    {
        new() {
            Icon = "⚡",
            Title = "Chuyển Power Plan",
            Description = "Chuyển sang chế độ tiết kiệm năng lượng",
            Action = "Vào tab Power Settings → Chọn Power Saver hoặc Balanced"
        },
        new() {
            Icon = "🔧",
            Title = "Giảm CPU Frequency",
            Description = "Hạ tốc độ CPU để giảm tải",
            Action = "Tab Power Settings → Giảm Max CPU Frequency xuống 70-80%"
        },
        new() {
            Icon = "🤖",
            Title = "Bật Smart Mode",
            Description = "Để hệ thống tự động điều chỉnh",
            Action = "Tab Power Settings → Enable Smart Mode"
        },
        new() {
            Icon = "🔄",
            Title = "Quản lý Process",
            Description = "Kiểm tra và đóng process tốn CPU",
            Action = "Tab Process Monitor → Tìm process CPU cao → End Process"
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
            Action = "Tab Power Settings → Set Max CPU Frequency = 60-70%"
        },
        new() {
            Icon = "🔋",
            Title = "Power Saver Mode",
            Description = "Chuyển sang chế độ tiết kiệm năng lượng",
            Action = "Tab Power Settings → Chọn Power Saver Plan"
        },
        new() {
            Icon = "🌡️",
            Title = "Kiểm tra tản nhiệt",
            Description = "Vệ sinh quạt CPU và tản nhiệt",
            Action = "Tắt máy → Vệ sinh quạt → Thay keo tản nhiệt"
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
            Action = "Tab Process Monitor → Sắp xếp theo Memory → End process không cần thiết"
        },
        new() {
            Icon = "⚠️",
            Title = "Giới hạn tài nguyên",
            Description = "Đặt giới hạn RAM cho process",
            Action = "Process Monitor → Right click process → Set Resource Limit"
        },
        new() {
            Icon = "🗑️",
            Title = "Dọn dẹp hệ thống",
            Description = "Giải phóng bộ nhớ không sử dụng",
            Action = "Chạy Disk Cleanup → Clear Temp files → Restart browser"
        }
    };
        }

        private List<SolutionRecommendation> GetGPUSolutions()
        {
            return new List<SolutionRecommendation>
    {
        new() {
            Icon = "🎮",
            Title = "Tối ưu Power Plan",
            Description = "Chuyển sang Balanced hoặc Power Saver",
            Action = "Tab Power Settings → Chọn Power Plan phù hợp"
        },
        new() {
            Icon = "🔄",
            Title = "Kiểm tra Process GPU",
            Description = "Tìm ứng dụng đang sử dụng GPU cao",
            Action = "Tab Process Monitor → Tìm process graphics-intensive → Đóng nếu không cần"
        },
        new() {
            Icon = "⚡",
            Title = "Giảm hiệu năng",
            Description = "Hạ settings đồ họa trong game/app",
            Action = "Giảm resolution, texture quality, disable effects"
        }
    };
        }

        private List<SolutionRecommendation> GetGPUTempSolutions()
        {
            return new List<SolutionRecommendation>
    {
        new() {
            Icon = "🔋",
            Title = "Power Management",
            Description = "Giảm power limit của GPU",
            Action = "Tab Power Settings → Chọn Power Saver để giảm GPU load"
        },
        new() {
            Icon = "🌪️",
            Title = "Tăng tốc quạt",
            Description = "Tăng fan curve GPU",
            Action = "MSI Afterburner → Tăng fan speed → Custom fan curve"
        },
        new() {
            Icon = "❄️",
            Title = "Vệ sinh làm mát",
            Description = "Thổi bụi GPU và case",
            Action = "Tắt máy → Compressed air → Vệ sinh GPU cooler"
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
            Action = "Tab Process Monitor → Tìm process I/O intensive → Tạm dừng hoặc đóng"
        },
        new() {
            Icon = "⚠️",
            Title = "Giới hạn I/O",
            Description = "Đặt giới hạn disk usage cho process",
            Action = "Process Monitor → Right click → Set Resource Limit cho disk usage"
        },
        new() {
            Icon = "⏸️",
            Title = "Tạm dừng backup/scan",
            Description = "Dừng antivirus hoặc backup đang chạy",
            Action = "Tạm dừng Windows Defender scan, OneDrive sync, backup software"
        },
        new() {
            Icon = "💾",
            Title = "Disk Sleep Settings",
            Description = "Tối ưu disk power management",
            Action = "Tab Power Settings → Điều chỉnh sleep settings cho disk"
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
        }
    };
        }

        public void CheckSystemOverload(SystemInfoEventArgs systemInfo)
        {
            var warnings = new System.Collections.Generic.List<string>();
            bool isOverloaded = false;

            // DEBUG: In ra thông tin hiện tại
            System.Diagnostics.Debug.WriteLine("=== DEBUG WARNING SYSTEM ===");
            System.Diagnostics.Debug.WriteLine($"Init time check: {DateTime.Now - _initTime} >= 5s = {DateTime.Now - _initTime >= TimeSpan.FromSeconds(5)}");
            System.Diagnostics.Debug.WriteLine($"CPU: {systemInfo.CpuUsage:F1}% (Threshold: {CpuWarningThreshold}%)");
            System.Diagnostics.Debug.WriteLine($"GPU: {systemInfo.GpuUsage:F1}% (Threshold: {GpuWarningThreshold}%)");
            System.Diagnostics.Debug.WriteLine($"GPU Temp: {systemInfo.GpuTemperature:F1}°C (Threshold: {GpuTemperatureThreshold}°C)");
            System.Diagnostics.Debug.WriteLine($"RAM: {systemInfo.RamUsage:F1}% (Threshold: {RamWarningThreshold}%)");
            System.Diagnostics.Debug.WriteLine($"Last warning: {DateTime.Now - _lastWarningTime} ago");
            System.Diagnostics.Debug.WriteLine($"Should show warning: {ShouldShowWarning()}");
            System.Diagnostics.Debug.WriteLine($"Background mode: {IsBackgroundMode}");

            // Wait 5 seconds after initialization
            if (DateTime.Now - _initTime < TimeSpan.FromSeconds(5))
            {
                System.Diagnostics.Debug.WriteLine("⏳ Đang trong thời gian chờ 5 giây khởi tạo");
                return;
            }

            // Existing warning checks với debug...
            if (systemInfo.CpuUsage > CpuWarningThreshold)
            {
                warnings.Add($"CPU: {systemInfo.CpuUsage:F1}%");
                isOverloaded = true;
                System.Diagnostics.Debug.WriteLine($"✅ CPU cảnh báo: {systemInfo.CpuUsage:F1}% > {CpuWarningThreshold}%");
            }

            if (systemInfo.CpuTemperature > CpuTemperatureThreshold)
            {
                warnings.Add($"CPU Temp: {systemInfo.CpuTemperature:F1}°C");
                isOverloaded = true;
                System.Diagnostics.Debug.WriteLine($"✅ CPU Temperature cảnh báo: {systemInfo.CpuTemperature:F1}°C > {CpuTemperatureThreshold}°C");
            }

            if (systemInfo.RamUsage > RamWarningThreshold)
            {
                warnings.Add($"RAM: {systemInfo.RamUsage:F1}%");
                isOverloaded = true;
                System.Diagnostics.Debug.WriteLine($"✅ RAM cảnh báo: {systemInfo.RamUsage:F1}% > {RamWarningThreshold}%");
            }

            if (systemInfo.GpuUsage > GpuWarningThreshold)
            {
                warnings.Add($"GPU: {systemInfo.GpuUsage:F1}%");
                isOverloaded = true;
                System.Diagnostics.Debug.WriteLine($"✅ GPU cảnh báo: {systemInfo.GpuUsage:F1}% > {GpuWarningThreshold}%");
            }

            if (systemInfo.GpuTemperature > GpuTemperatureThreshold)
            {
                warnings.Add($"GPU Temp: {systemInfo.GpuTemperature:F1}°C");
                isOverloaded = true;
                System.Diagnostics.Debug.WriteLine($"✅ GPU Temperature cảnh báo: {systemInfo.GpuTemperature:F1}°C > {GpuTemperatureThreshold}°C");
            }

            if (systemInfo.DiskUsage > DiskWarningThreshold)
            {
                warnings.Add($"Disk: {systemInfo.DiskUsage:F1}%");
                isOverloaded = true;
                System.Diagnostics.Debug.WriteLine($"✅ Disk cảnh báo: {systemInfo.DiskUsage:F1}% > {DiskWarningThreshold}%");
            }

            System.Diagnostics.Debug.WriteLine($"Tổng cảnh báo: {warnings.Count}, isOverloaded: {isOverloaded}");

            // Handle warnings based on mode
            if (isOverloaded && ShouldShowWarning())
            {
                System.Diagnostics.Debug.WriteLine("🚨 KÍCH HOẠT CẢNH BÁO!");

                if (IsBackgroundMode)
                {
                    // Background mode: only trigger event
                    string warningText = string.Join(", ", warnings);
                    System.Diagnostics.Debug.WriteLine($"📱 Background warning: {warningText}");
                    WarningTriggered?.Invoke(this, warningText);
                }
                else
                {
                    // Foreground mode: update UI and show popup
                    System.Diagnostics.Debug.WriteLine("🖥️ Foreground warning - Updating UI");
                    App.Current?.Dispatcher.Invoke(() =>
                    {
                        UpdateWarningUI(isOverloaded, warnings);
                    });
                    ShowWarningPopup(warnings);
                }
            }
            else if (!IsBackgroundMode)
            {
                // Only update UI when not in background mode
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

        private void ShowWarningPopup(List<string> warnings)
        {
            _lastWarningTime = DateTime.Now;
            var solutions = GetSpecificSolutions(warnings);

            string message = "⚠️ HỆ THỐNG CẢNH BÁO!\n\n";
            message += "🔍 VẤN ĐỀ PHÁT HIỆN:\n";

            foreach (var warning in warnings)
            {
                message += $"• {warning}\n";
            }

            message += "\n💡 GIẢI PHÁP ĐỀ XUẤT:\n";

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
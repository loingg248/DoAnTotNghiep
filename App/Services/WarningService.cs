using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SystemMonitor.Services
{
    public class WarningService
    {
        // Ngưỡng cảnh báo (có thể điều chỉnh)
        public float CpuWarningThreshold { get; set; } = 85f;
        public float CpuTemperatureThreshold { get; set; } = 80f;
        public float RamWarningThreshold { get; set; } = 90f;
        public float GpuWarningThreshold { get; set; } = 85f;
        public float GpuTemperatureThreshold { get; set; } = 85f;
        public float DiskWarningThreshold { get; set; } = 95f;

        // UI Controls
        private TextBlock _warningStatus;
        private Border _warningBorder;

        // Trạng thái cảnh báo
        private bool _isWarningActive = false;
        private DateTime _lastWarningTime = DateTime.MinValue;
        private readonly TimeSpan _warningCooldown = TimeSpan.FromMinutes(2);
        private DateTime _initTime = DateTime.Now;

        public void SetUIControls(TextBlock warningStatus, Border warningBorder)
        {
            _warningStatus = warningStatus;
            _warningBorder = warningBorder;
        }

        public void CheckSystemOverload(SystemInfoEventArgs systemInfo)
        {
            var warnings = new System.Collections.Generic.List<string>();
            bool isOverloaded = false;

            // Chờ 5 giây sau khi khởi tạo
            if (DateTime.Now - _initTime < TimeSpan.FromSeconds(5))
            {
                return;
            }

            // Kiểm tra CPU
            if (systemInfo.CpuUsage > CpuWarningThreshold)
            {
                warnings.Add($"CPU: {systemInfo.CpuUsage:F1}%");
                isOverloaded = true;
            }

            // Kiểm tra nhiệt độ CPU
            if (systemInfo.CpuTemperature > CpuTemperatureThreshold)
            {
                warnings.Add($"CPU Temp: {systemInfo.CpuTemperature:F1}°C");
                isOverloaded = true;
            }

            // Kiểm tra RAM
            if (systemInfo.RamUsage > RamWarningThreshold)
            {
                warnings.Add($"RAM: {systemInfo.RamUsage:F1}%");
                isOverloaded = true;
            }

            // Kiểm tra GPU
            if (systemInfo.GpuUsage > GpuWarningThreshold)
            {
                warnings.Add($"GPU: {systemInfo.GpuUsage:F1}%");
                isOverloaded = true;
            }

            // Kiểm tra nhiệt độ GPU
            if (systemInfo.GpuTemperature > GpuTemperatureThreshold)
            {
                warnings.Add($"GPU Temp: {systemInfo.GpuTemperature:F1}°C");
                isOverloaded = true;
            }

            // Kiểm tra Disk
            if (systemInfo.DiskUsage > DiskWarningThreshold)
            {
                warnings.Add($"Disk: {systemInfo.DiskUsage:F1}%");
                isOverloaded = true;
            }

            // Cập nhật UI và hiển thị cảnh báo
            App.Current?.Dispatcher.Invoke(() =>
            {
                UpdateWarningUI(isOverloaded, warnings);
            });

            // Hiển thị popup cảnh báo nếu cần
            if (isOverloaded && ShouldShowWarning())
            {
                ShowWarningPopup(warnings);
            }
        }

        private void UpdateWarningUI(bool isOverloaded, System.Collections.Generic.List<string> warnings)
        {
            if (_warningStatus == null || _warningBorder == null) return;

            if (isOverloaded)
            {
                _isWarningActive = true;

                // Hiển thị chi tiết các vấn đề thay vì chỉ số lượng
                string warningText = "⚠️ Vấn đề phát hiện: " + string.Join(" | ", warnings);
                _warningStatus.Text = warningText;

                _warningBorder.Background = new SolidColorBrush(Color.FromRgb(255, 243, 205)); // Màu vàng nhạt
                _warningBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Màu vàng đậm
                _warningBorder.BorderThickness = new Thickness(2);
                _warningBorder.Visibility = Visibility.Visible;

                // Tooltip với thông tin chi tiết hơn
                string tooltipText = "🚨 CHI TIẾT CẢNH BÁO:\n\n";
                foreach (var warning in warnings)
                {
                    tooltipText += "• " + warning + "\n";
                }
                tooltipText += "\n💡 Khuyến nghị: Kiểm tra và giảm tải các thành phần đang quá mức.";
                _warningBorder.ToolTip = tooltipText;
            }
            else
            {
                if (_isWarningActive)
                {
                    _isWarningActive = false;
                    _warningStatus.Text = "✅ Hệ thống hoạt động bình thường";
                    _warningBorder.Background = new SolidColorBrush(Color.FromRgb(212, 237, 218)); // Màu xanh nhạt  
                    _warningBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(25, 135, 84)); // Màu xanh đậm
                    _warningBorder.BorderThickness = new Thickness(1);
                    _warningBorder.ToolTip = "Tất cả thông số hệ thống đều ở mức bình thường";
                }
                else
                {
                    _warningBorder.Visibility = Visibility.Collapsed; // Ẩn khi không có vấn đề
                }
            }
        }

        private bool ShouldShowWarning()
        {
            return DateTime.Now - _lastWarningTime > _warningCooldown;
        }

        private void ShowWarningPopup(System.Collections.Generic.List<string> warnings)
        {
            _lastWarningTime = DateTime.Now;

            string message = "⚠️ CẢNH BÁO HỆ THỐNG!\n\n";
            message += "🔍 CÁC VẤN ĐỀ PHÁT HIỆN:\n";

            foreach (var warning in warnings)
            {
                message += $"• {warning}\n";
            }

            message += "\n💡 KHUYẾN NGHỊ:\n";
            message += "• Đóng các ứng dụng không cần thiết\n";
            message += "• Kiểm tra tản nhiệt hệ thống\n";
            message += "• Giảm cường độ hoạt động của CPU/GPU\n";
            message += "• Dọn dẹp RAM và ổ cứng nếu cần";

            MessageBox.Show(message, "Cảnh báo hệ thống", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Phương thức để người dùng tùy chỉnh ngưỡng cảnh báo
        public void UpdateThresholds(float cpuThreshold, float cpuTempThreshold, float ramThreshold,
                                   float gpuThreshold, float gpuTempThreshold, float diskThreshold)
        {
            CpuWarningThreshold = Math.Max(50, Math.Min(100, cpuThreshold));
            CpuTemperatureThreshold = Math.Max(60, Math.Min(100, cpuTempThreshold));
            RamWarningThreshold = Math.Max(70, Math.Min(100, ramThreshold));
            GpuWarningThreshold = Math.Max(50, Math.Min(100, gpuThreshold));
            GpuTemperatureThreshold = Math.Max(60, Math.Min(100, gpuTempThreshold));
            DiskWarningThreshold = Math.Max(80, Math.Min(100, diskThreshold));
        }
    }
}
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

        // Existing properties...
        public float CpuWarningThreshold { get; set; } = 85f;
        public float CpuTemperatureThreshold { get; set; } = 80f;
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

        public void CheckSystemOverload(SystemInfoEventArgs systemInfo)
        {
            var warnings = new System.Collections.Generic.List<string>();
            bool isOverloaded = false;

            // Wait 5 seconds after initialization
            if (DateTime.Now - _initTime < TimeSpan.FromSeconds(5))
            {
                return;
            }

            // Existing warning checks...
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

            // Handle warnings based on mode
            if (isOverloaded && ShouldShowWarning())
            {
                if (IsBackgroundMode)
                {
                    // Background mode: only trigger event
                    string warningText = string.Join(", ", warnings);
                    WarningTriggered?.Invoke(this, warningText);
                }
                else
                {
                    // Foreground mode: update UI and show popup
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
            }
        }

        // Existing methods remain the same...
        private void UpdateWarningUI(bool isOverloaded, System.Collections.Generic.List<string> warnings)
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

                string tooltipText = "🚨 WARNING DETAILS:\n\n";
                foreach (var warning in warnings)
                {
                    tooltipText += "• " + warning + "\n";
                }
                tooltipText += "\n💡 Recommendation: Check and reduce load on overloaded components.";
                _warningBorder.ToolTip = tooltipText;
            }
            else
            {
                if (_isWarningActive)
                {
                    _isWarningActive = false;
                    _warningStatus.Text = "✅ System operating normally";
                    _warningBorder.Background = new SolidColorBrush(Color.FromRgb(212, 237, 218));
                    _warningBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(25, 135, 84));
                    _warningBorder.BorderThickness = new Thickness(1);
                    _warningBorder.ToolTip = "All system parameters are at normal levels";
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

        private void ShowWarningPopup(System.Collections.Generic.List<string> warnings)
        {
            _lastWarningTime = DateTime.Now;

            string message = "⚠️ SYSTEM WARNING!\n\n";
            message += "🔍 DETECTED ISSUES:\n";

            foreach (var warning in warnings)
            {
                message += $"• {warning}\n";
            }

            message += "\n💡 RECOMMENDATIONS:\n";
            message += "• Close unnecessary applications\n";
            message += "• Check system cooling\n";
            message += "• Reduce CPU/GPU workload intensity\n";
            message += "• Clean up RAM and hard drive if needed";

            MessageBox.Show(message, "System Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

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
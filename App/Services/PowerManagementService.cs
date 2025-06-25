using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SystemMonitor.Models;
using SystemMonitor.Services;

namespace SystemMonitor.Services
{
    public class PowerManagementService
    {
        private MonitoringService _monitoringService;

        public TextBlock AutoAdjustStatus { get; set; }

        public int savedMinFrequency { get; set; } = 5;
        public int savedMaxFrequency { get; set; } = 100;

        public PowerManagementService()
        {
        }

        public void SetMonitoringService(MonitoringService monitoringService)
        {
            _monitoringService = monitoringService;
        }

        public void SetUIControls(TextBlock autoAdjustStatus)
        {
            AutoAdjustStatus = autoAdjustStatus;
        }

        public void LoadPowerPlans(ComboBox powerPlanComboBox)
        {
            try
            {
                var powerPlans = new List<PowerPlan>();

                ProcessStartInfo psi = new ProcessStartInfo("powercfg", "/list")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        string[] lines = output.Split('\n');
                        foreach (string line in lines)
                        {
                            if (line.Contains("Power Scheme GUID:"))
                            {
                                // Phân tích thông tin kế hoạch nguồn
                                string[] parts = line.Split(' ');
                                if (parts.Length >= 4)
                                {
                                    string guid = parts[3];
                                    string name = line.Substring(line.IndexOf('(') + 1);
                                    name = name.Substring(0, name.IndexOf(')'));
                                    bool isActive = line.Contains("*");

                                    powerPlans.Add(new PowerPlan
                                    {
                                        Guid = guid,
                                        Name = name,
                                        IsActive = isActive
                                    });
                                }
                            }
                        }
                    }
                }

                powerPlanComboBox.ItemsSource = powerPlans;
                powerPlanComboBox.DisplayMemberPath = "Name";
                powerPlanComboBox.SelectedValuePath = "Guid";

                foreach (PowerPlan plan in powerPlans)
                {
                    if (plan.IsActive)
                    {
                        powerPlanComboBox.SelectedValue = plan.Guid;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải danh sách kế hoạch nguồn: {ex.Message}",
                               "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ChangePowerPlan(string guid)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("powercfg", $"/setactive {guid}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        process.WaitForExit();
                        if (process.ExitCode == 0)
                        {
                            MessageBox.Show("Đã thay đổi kế hoạch nguồn thành công!",
                                           "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thay đổi kế hoạch nguồn: {ex.Message}",
                               "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task SetCpuFrequency(int minFreq, int maxFreq)
        {
            try
            {
                string currentPlanGuid = GetActivePowerPlanGuid();

                if (string.IsNullOrEmpty(currentPlanGuid))
                    return;

                ProcessStartInfo psi = new ProcessStartInfo("powercfg",
                    $"/setacvalueindex {currentPlanGuid} sub_processor PROCTHROTTLEMIN {minFreq}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        await Task.Run(() => process.WaitForExit());
                    }
                }

                psi = new ProcessStartInfo("powercfg",
                    $"/setacvalueindex {currentPlanGuid} sub_processor PROCTHROTTLEMAX {maxFreq}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        await Task.Run(() => process.WaitForExit());
                    }
                }

                psi = new ProcessStartInfo("powercfg", $"/setactive {currentPlanGuid}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        await Task.Run(() => process.WaitForExit());
                    }
                }

                savedMinFrequency = minFreq;
                savedMaxFrequency = maxFreq;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi khi thiết lập tần số CPU: {ex.Message}");
            }
        }

        private string GetActivePowerPlanGuid()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("powercfg", "/getactivescheme")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        string[] parts = output.Split(' ');
                        if (parts.Length >= 4)
                        {
                            return parts[3].Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi khi lấy kế hoạch nguồn đang hoạt động: {ex.Message}");
            }
            return null;
        }

        public void LoadCurrentCpuFrequencies()
        {
            try
            {
                string activePlanGuid = GetActivePowerPlanGuid();
                if (string.IsNullOrEmpty(activePlanGuid))
                    return;

                ProcessStartInfo psi = new ProcessStartInfo("powercfg",
                    $"/query {activePlanGuid} sub_processor PROCTHROTTLEMIN")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        string[] lines = output.Split('\n');
                        foreach (string line in lines)
                        {
                            if (line.Contains("Current AC Power Setting Index:"))
                            {
                                string[] parts = line.Split(':');
                                if (parts.Length > 1)
                                {
                                    string hexValue = parts[1].Trim().Replace("0x", "");
                                    if (int.TryParse(hexValue, System.Globalization.NumberStyles.HexNumber, null, out int minFreq))
                                    {
                                        savedMinFrequency = minFreq;
                                    }
                                }
                                break;
                            }
                        }
                    }
                }

                psi = new ProcessStartInfo("powercfg",
                    $"/query {activePlanGuid} sub_processor PROCTHROTTLEMAX")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        string[] lines = output.Split('\n');
                        foreach (string line in lines)
                        {
                            if (line.Contains("Current AC Power Setting Index:"))
                            {
                                string[] parts = line.Split(':');
                                if (parts.Length > 1)
                                {
                                    string hexValue = parts[1].Trim().Replace("0x", "");
                                    if (int.TryParse(hexValue, System.Globalization.NumberStyles.HexNumber, null, out int maxFreq))
                                    {
                                        savedMaxFrequency = maxFreq;
                                    }
                                }
                                break;
                            }
                        }
                    }
                }

                Debug.WriteLine($"Đã tải tần số CPU - Tối thiểu: {savedMinFrequency}%, Tối đa: {savedMaxFrequency}%");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi khi tải tần số CPU hiện tại: {ex.Message}");
                savedMinFrequency = 5;
                savedMaxFrequency = 100;
            }
        }

        public async void StartAutoAdjustCpu(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_monitoringService?.cpuUsageCounter != null)
                    {
                        float cpuUsage = _monitoringService.cpuUsageCounter.NextValue();
                        Debug.WriteLine($"[Chế độ thông minh] Sử dụng CPU: {cpuUsage:F1}%");

                        int minFreq = 0;
                        int maxFreq = 100;
                        string mode = "";

                        // Logic chế độ thông minh dựa trên ngưỡng sử dụng CPU
                        if (cpuUsage <= 30) // Sử dụng thấp (0-30%): Chế độ tiết kiệm năng lượng
                        {
                            minFreq = 5;
                            maxFreq = 40;
                            mode = "Chế độ tiết kiệm năng lượng";
                        }
                        else if (cpuUsage > 30 && cpuUsage <= 70) // Sử dụng trung bình (30-70%): Chế độ cân bằng
                        {
                            minFreq = 20;
                            maxFreq = 70;
                            mode = "Chế độ cân bằng";
                        }
                        else // Sử dụng cao (70%+): Chế độ hiệu suất
                        {
                            minFreq = 50;
                            maxFreq = 100;
                            mode = "Chế độ hiệu suất";
                        }

                        // Chỉ điều chỉnh nếu giá trị tần số đã thay đổi
                        if (minFreq != savedMinFrequency || maxFreq != savedMaxFrequency)
                        {
                            await SetCpuFrequency(minFreq, maxFreq);

                            if (AutoAdjustStatus != null)
                            {
                                App.Current.Dispatcher.Invoke(() => {
                                    AutoAdjustStatus.Text = $"Chế độ thông minh: {mode} - {minFreq}%-{maxFreq}% (CPU: {cpuUsage:F1}%)";
                                });
                            }

                            Debug.WriteLine($"[Chế độ thông minh] Chuyển sang {mode}: {minFreq}%-{maxFreq}%");
                        }
                    }

                    // Kiểm tra mỗi 2 giây để điều chỉnh linh hoạt hơn
                    await Task.Delay(2000, cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                // Mong đợi khi yêu cầu hủy bỏ
                Debug.WriteLine("[Chế độ thông minh] Điều chỉnh tự động đã bị hủy");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Chế độ thông minh] Lỗi trong điều chỉnh tự động CPU: {ex.Message}");

                if (AutoAdjustStatus != null)
                {
                    App.Current.Dispatcher.Invoke(() => {
                        AutoAdjustStatus.Text = $"Lỗi chế độ thông minh: {ex.Message}";
                    });
                }
            }
            finally
            {
                Debug.WriteLine("[Chế độ thông minh] Điều chỉnh tự động CPU đã dừng");

                if (AutoAdjustStatus != null)
                {
                    App.Current.Dispatcher.Invoke(() => {
                        AutoAdjustStatus.Text = "Chế độ thông minh đã dừng - Điều chỉnh tự động đang bị vô hiệu hóa";
                    });
                }
            }
        }
    }
}
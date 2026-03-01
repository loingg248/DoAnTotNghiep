using App.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        public TextBlock SmartModeStatus { get; set; }

        public int savedMinFrequency { get; set; } = 5;
        public int savedMaxFrequency { get; set; } = 100;

        private SmartModeML _smartML = new SmartModeML(); 
        private bool _mlReady = false;

        public PowerManagementService()
        {
        }

        public void SetMonitoringService(MonitoringService monitoringService)
        {
            _monitoringService = monitoringService;
        }

        public void SetUIControls(TextBlock autoAdjustStatus)
        {
            SmartModeStatus = autoAdjustStatus;
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
            string lastState = ""; // Lưu trạng thái trước đó

            try
            {
                if (!_mlReady)
                {
                    if (File.Exists("Logs/system_usage.csv"))
                    {
                        var lines = File.ReadAllLines("Logs/system_usage.csv");
                        if (lines.Length > 10)
                        {
                            _smartML.Train("Logs/system_usage.csv");
                            _mlReady = true;
                        }
                        else
                        {
                            Debug.WriteLine("[Smart Mode ML] Chưa đủ dữ liệu để huấn luyện.");
                        }
                    }
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_monitoringService?.cpuUsageCounter != null && _mlReady)
                    {
                        float cpuUsage = _monitoringService.cpuUsageCounter.NextValue();
                        float gpuUsage = _monitoringService?.gpuUsageCounter?.NextValue() ?? 0;
                        float ramUsage = _monitoringService?.availableMemoryCounter != null
                            ? 100 - (_monitoringService.availableMemoryCounter.NextValue() / 1024f / _monitoringService.GetTotalRamGB() * 100)
                            : 0;

                        var input = new SystemUsageData
                        {
                            CpuUsage = cpuUsage,
                            GpuUsage = gpuUsage,
                            RamUsage = ramUsage
                        };

                        string predictedState = _smartML.Predict(input);

                        // Chỉ xử lý khi trạng thái thay đổi
                        if (predictedState != lastState)
                        {
                            int minFreq = 0;
                            int maxFreq = 0;
                            string icon = "";

                            switch (predictedState)
                            {
                                case "Gaming":
                                    minFreq = 50;
                                    maxFreq = 100;
                                    icon = "🎮";
                                    break;
                                case "Office":
                                    minFreq = 20;
                                    maxFreq = 70;
                                    icon = "💼";
                                    break;
                                case "Idle":
                                    minFreq = 5;
                                    maxFreq = 40;
                                    icon = "🌙";
                                    break;
                                default:
                                    icon = "🤖";
                                    break;
                            }

                            await SetCpuFrequency(minFreq, maxFreq);

                            App.Current.Dispatcher.Invoke(() =>
                            {
                                SmartModeStatus.Text = $"{icon} Chế độ hiện tại: {predictedState}";
                            });

                            Debug.WriteLine($"[Smart Mode ML] Chuyển từ {lastState} sang {predictedState}");
                            lastState = predictedState;
                        }
                    }

                    // Giảm tần suất xuống 5 giây để nhẹ hơn
                    await Task.Delay(5000, cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("[Smart Mode ML] Auto adjustment cancelled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Smart Mode ML] Error: {ex.Message}");
                App.Current.Dispatcher.Invoke(() =>
                {
                    SmartModeStatus.Text = $"Smart Mode ML Error: {ex.Message}";
                });
            }
            finally
            {
                Debug.WriteLine("[Smart Mode ML] Auto adjustment stopped.");
                App.Current.Dispatcher.Invoke(() =>
                {
                    SmartModeStatus.Text = "Smart Mode ML đã dừng.";
                });
            }
        }



    }
}
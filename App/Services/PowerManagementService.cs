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
                                // Parse power plan information
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
                MessageBox.Show($"Lỗi khi tải danh sách power plans: {ex.Message}",
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
                            MessageBox.Show("Đã thay đổi power plan thành công!",
                                           "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thay đổi power plan: {ex.Message}",
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
                Debug.WriteLine($"Error setting CPU frequency: {ex.Message}");
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
                Debug.WriteLine($"Error getting active power plan: {ex.Message}");
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

                Debug.WriteLine($"Loaded CPU frequencies - Min: {savedMinFrequency}%, Max: {savedMaxFrequency}%");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading current CPU frequencies: {ex.Message}");
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
                        Debug.WriteLine($"[Smart Mode] CPU Usage: {cpuUsage:F1}%");

                        int minFreq = 0;
                        int maxFreq = 100;
                        string mode = "";

                        // Smart Mode logic based on CPU usage thresholds
                        if (cpuUsage <= 30) // Low usage (0-30%): Power saving mode
                        {
                            minFreq = 5;
                            maxFreq = 40;
                            mode = "Power saving mode";
                        }
                        else if (cpuUsage > 30 && cpuUsage <= 70) // Medium usage (30-70%): Balanced mode
                        {
                            minFreq = 20;
                            maxFreq = 70;
                            mode = "Balanced mode";
                        }
                        else // High usage (70%+): Performance mode
                        {
                            minFreq = 50;
                            maxFreq = 100;
                            mode = "Performance mode";
                        }

                        // Only adjust if frequency values have changed
                        if (minFreq != savedMinFrequency || maxFreq != savedMaxFrequency)
                        {
                            await SetCpuFrequency(minFreq, maxFreq);

                            if (AutoAdjustStatus != null)
                            {
                                App.Current.Dispatcher.Invoke(() => {
                                    AutoAdjustStatus.Text = $"Smart Mode: {mode} - {minFreq}%-{maxFreq}% (CPU: {cpuUsage:F1}%)";
                                });
                            }

                            Debug.WriteLine($"[Smart Mode] Switched to {mode}: {minFreq}%-{maxFreq}%");
                        }
                    }

                    // Check every 2 seconds for more responsive adjustments
                    await Task.Delay(2000, cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation is requested
                Debug.WriteLine("[Smart Mode] Auto adjustment cancelled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Smart Mode] Error in auto CPU adjustment: {ex.Message}");

                if (AutoAdjustStatus != null)
                {
                    App.Current.Dispatcher.Invoke(() => {
                        AutoAdjustStatus.Text = $"Smart Mode error: {ex.Message}";
                    });
                }
            }
            finally
            {
                Debug.WriteLine("[Smart Mode] Auto CPU adjustment stopped");

                if (AutoAdjustStatus != null)
                {
                    App.Current.Dispatcher.Invoke(() => {
                        AutoAdjustStatus.Text = "Smart Mode đã dừng - Automatic adjustment is disabled";
                    });
                }
            }
        }
    }
}
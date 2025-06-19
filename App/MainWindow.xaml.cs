using System;
using System.Threading;
using System.Windows;
using SystemMonitor.Models;
using SystemMonitor.Services;
using SystemMonitor.Helpers;
using System.Windows.Controls;
using LibreHardwareMonitor.Hardware;
using System.Diagnostics;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Data;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Threading;


namespace SystemMonitor
{
    public partial class MainWindow : Window
    {
        private TaskbarIcon _notifyIcon;
        private bool _isReallyClosing = false;
        private DispatcherTimer _backgroundTimer;
        private DispatcherTimer _mainTimer;
        private bool _isBackgroundMode = false;

        private bool isInitializing = true;

        private GridViewColumnHeader _lastHeaderClicked = null;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;

        private readonly MonitoringService _monitoringService;
        private readonly PowerManagementService _powerManagementService;
        private readonly ChartService _chartService;
        private readonly WarningService _warningService;
        private readonly ProcessService _processService;


        private CancellationTokenSource? cancellationTokenSource;
        private bool isMonitoring = false;
        private bool isAutoAdjustEnabled = false;
        private CancellationTokenSource? autoAdjustCancellationTokenSource;
        private string currentPowerPlan;


        public MainWindow()
        {
            InitializeComponent();
            InitializeBackgroundMode();
            

            _monitoringService = new MonitoringService();
            _powerManagementService = new PowerManagementService();
            _chartService = new ChartService(CpuChart, RamChart, GpuChart, DiskChart);
            _warningService = new WarningService();
            _warningService.SetUIControls(WarningStatus, WarningBorder);
            _processService = new ProcessService();
            _processService.SetUIControls(ProcessListView, ProcessCountLabel);

            InitializeServices();

            _chartService.SetUIControls(
                CpuLoad,
                RamUsage,
                GpuLoad,
                DiskActivity,
                CurrentMinCpuFreq,
                CurrentMaxCpuFreq
            );

            _chartService.SetPowerManagementService(_powerManagementService);

            _powerManagementService.SetUIControls(AutoAdjustStatus);

            _powerManagementService.SetMonitoringService(_monitoringService);

            _monitoringService.SetUIControls(
                CpuName, CpuTemp, CpuLoad, CpuClock,
                RamUsage, RamUsed, RamAvailable, RamTotal,
                GpuName, GpuTemp, GpuLoad, GpuMemory,
                DiskName, DiskActivity, DiskTemp, DiskSpace);

            _monitoringService.DataUpdated += UpdateUI;

            _powerManagementService.LoadPowerPlans(PowerPlanComboBox);
            _monitoringService.RefreshSystemInfo();

            currentPowerPlan = GetCurrentActivePowerPlan();

            _powerManagementService.LoadCurrentCpuFrequencies();

            _chartService.UpdateDvfsInfo();

            isInitializing = false;

            // Automatically start monitoring after initialization
            AutoStartMonitoring();
        }

        private void InitializeBackgroundMode()
        {
            // Lấy NotifyIcon từ resources
            _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");

            // Tạo timer cho chế độ chạy ngầm
            _backgroundTimer = new DispatcherTimer();
            _backgroundTimer.Interval = TimeSpan.FromSeconds(5); // 5 giây
            _backgroundTimer.Tick += BackgroundTimer_Tick;
        }

        private void InitializeServices()
        {
            // Cấu hình warning service cho chế độ nền
            _warningService.WarningTriggered += OnBackgroundWarning;

            // Cấu hình UI controls cho warning service
            _warningService.SetUIControls(WarningStatus, WarningBorder);
        }

        // Event handlers cho Window
        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                // Ẩn window và hiển thị tray icon
                Hide();
                _notifyIcon.Visibility = Visibility.Visible;

                // Hiển thị thông báo
                _notifyIcon.ShowBalloonTip("System Monitor",
                    "Application minimized to system tray. Monitoring continues in background.",
                    BalloonIcon.Info);

                // Chuyển sang chế độ chạy ngầm
                StartBackgroundMode();
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!_isReallyClosing)
            {
                // Ngăn đóng app, chuyển sang chế độ chạy ngầm
                e.Cancel = true;
                WindowState = WindowState.Minimized;
            }
        }

        // Methods cho chế độ chạy ngầm
        private void StartBackgroundMode()
        {
            _isBackgroundMode = true;
            _warningService.SetBackgroundMode(true);

            // Dừng timer chính để tiết kiệm tài nguyên
            _mainTimer?.Stop();

            // Bắt đầu timer chạy ngầm
            _backgroundTimer.Start();
        }

        private void StopBackgroundMode()
        {
            _isBackgroundMode = false;
            _warningService.SetBackgroundMode(false);

            // Dừng timer chạy ngầm
            _backgroundTimer.Stop();

            // Ẩn tray icon
            _notifyIcon.Visibility = Visibility.Collapsed;

            // Khởi động lại timer chính
            _mainTimer?.Start();
        }

        private void BackgroundTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Chỉ kiểm tra cảnh báo hệ thống, không cập nhật UI
                var systemInfo = GetCurrentSystemInfo();
                _warningService.CheckSystemOverload(systemInfo);
            }
            catch (Exception ex)
            {
                // Log error nếu cần
                System.Diagnostics.Debug.WriteLine($"Background monitoring error: {ex.Message}");
            }
        }

        private void OnBackgroundWarning(object sender, string warningMessage)
        {
            // Hiển thị cảnh báo qua tray notification
            Dispatcher.Invoke(() =>
            {
                _notifyIcon.ShowBalloonTip("⚠️ System Warning",
                    warningMessage, BalloonIcon.Warning);
            });
        }

        private void ShowMonitor_Click(object sender, RoutedEventArgs e)
        {
            ShowFromTray();
        }

        private void ShowAlertSettings_Click(object sender, RoutedEventArgs e)
        {
            ShowFromTray();
            // Chuyển đến tab Alerts
            if (sender is TabControl tabControl)
            {
                tabControl.SelectedIndex = 2; // Index của tab Alerts
            }
        }

        private void ExitApplication_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Focus();
            StopBackgroundMode();
        }

        private void ExitApplication()
        {
            _isReallyClosing = true;
            _backgroundTimer?.Stop();
            _mainTimer?.Stop();
            _notifyIcon?.Dispose();
            Application.Current.Shutdown();
        }

        private SystemInfoEventArgs GetCurrentSystemInfo()
        {
            // Implement logic lấy thông tin hệ thống tương tự như timer chính
            // Nhưng không cập nhật UI, chỉ lấy dữ liệu

            return new SystemInfoEventArgs
            {
                CpuUsage = GetCpuUsage(),
                CpuTemperature = GetCpuTemperature(),
                RamUsage = GetRamUsage(),
                GpuUsage = GetGpuUsage(),
                GpuTemperature = GetGpuTemperature(),
                DiskUsage = GetDiskUsage()
            };
        }

        private float GetCpuUsage()
        {
            // Copy logic từ timer chính của bạn
            return 0; // Placeholder
        }

        private float GetCpuTemperature()
        {
            // Copy logic từ timer chính của bạn
            return 0; // Placeholder
        }

        private float GetRamUsage()
        {
            // Copy logic từ timer chính của bạn
            return 0; // Placeholder
        }

        private float GetGpuUsage()
        {
            // Copy logic từ timer chính của bạn
            return 0; // Placeholder
        }

        private float GetGpuTemperature()
        {
            // Copy logic từ timer chính của bạn
            return 0; // Placeholder
        }

        private float GetDiskUsage()
        {
            // Copy logic từ timer chính của bạn
            return 0; // Placeholder
        }

        public void SetMainTimer(DispatcherTimer mainTimer)
        {
            _mainTimer = mainTimer;
        }

        private void AutoStartMonitoring()
        {
            try
            {
                if (!isMonitoring)
                {
                    isMonitoring = true;
                    cancellationTokenSource = new CancellationTokenSource();
                    _monitoringService.StartMonitoring(cancellationTokenSource.Token);

                    // Update UI to reflect monitoring status
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error when automatically starting monitoring: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string GetCurrentActivePowerPlan()
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
                MessageBox.Show($"Error getting current power plan: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return string.Empty;
        }

        private void UpdateUI(object sender, SystemInfoEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateUI(sender, e));
                return;
            }

            _chartService.UpdateCharts(e.CpuUsage, e.RamUsage, e.GpuUsage, e.DiskUsage);
            _warningService.CheckSystemOverload(e);
        }

        private void SaveWarningSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                float cpuThreshold = float.Parse(CpuThresholdTextBox.Text);
                float cpuTempThreshold = float.Parse(CpuTempThresholdTextBox.Text);
                float ramThreshold = float.Parse(RamThresholdTextBox.Text);
                float gpuThreshold = float.Parse(GpuThresholdTextBox.Text);
                float gpuTempThreshold = float.Parse(GpuTempThresholdTextBox.Text);
                float diskThreshold = float.Parse(DiskThresholdTextBox.Text);

                _warningService.UpdateThresholds(cpuThreshold, cpuTempThreshold, ramThreshold,
                                               gpuThreshold, gpuTempThreshold, diskThreshold);

                WarningSettingsStatus.Text = $"✅ Settings saved: CPU {cpuThreshold}%, RAM {ramThreshold}%, GPU {gpuThreshold}%";
                WarningSettingsStatus.Foreground = new SolidColorBrush(Colors.Green);

                MessageBox.Show("Warning settings have been saved successfully!", "Success",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WarningSettingsStatus.Text = "❌ Error: Please enter valid numbers";
                WarningSettingsStatus.Foreground = new SolidColorBrush(Colors.Red);

                MessageBox.Show($"Error saving settings: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _monitoringService.RefreshSystemInfo();
        }

        private void StartMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isMonitoring)
            {
                isMonitoring = true;
                cancellationTokenSource = new CancellationTokenSource();
                _monitoringService.StartMonitoring(cancellationTokenSource.Token);

                // Update UI if needed
                // StartMonitorButton.Content = "⏹ Stop Monitoring";
            }
        }

        private void StopMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            if (isMonitoring)
            {
                isMonitoring = false;
                cancellationTokenSource?.Cancel();

                // Update UI if needed
                // StartMonitorButton.Content = "▶ Start Monitoring";
            }
        }

        private void PowerPlanComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing)
                return;
            if (PowerPlanComboBox.SelectedValue != null)
            {
                string selectedGuid = PowerPlanComboBox.SelectedValue.ToString();
                if (!string.IsNullOrEmpty(selectedGuid) && selectedGuid != currentPowerPlan)
                {
                    _powerManagementService.ChangePowerPlan(selectedGuid);
                    currentPowerPlan = selectedGuid;

                    _powerManagementService.LoadCurrentCpuFrequencies();
                    _chartService.UpdateDvfsInfo();
                }
            }
        }

        private void AutoAdjustCpuButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isAutoAdjustEnabled)
            {
                isAutoAdjustEnabled = true;
                AutoAdjustCpuButton.Content = "⏹ Stop Auto Adjustment";
                autoAdjustCancellationTokenSource = new CancellationTokenSource();
                _powerManagementService.StartAutoAdjustCpu(autoAdjustCancellationTokenSource.Token);
            }
            else
            {
                isAutoAdjustEnabled = false;
                AutoAdjustCpuButton.Content = "🤖 Start Auto Adjustment";
                autoAdjustCancellationTokenSource?.Cancel();
            }
        }

        public void SetMinCpuFrequency_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (int.TryParse(MinFrequencyTextBox.Text, out int minFrequency) && minFrequency > 0 && minFrequency <= 100)
                {
                    string activePlanGuid = GetCurrentActivePowerPlan();
                    if (string.IsNullOrEmpty(activePlanGuid))
                    {
                        MessageBox.Show("Cannot get current power plan information", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    ProcessStartInfo psi = new ProcessStartInfo("powercfg",
                        $"/setacvalueindex {activePlanGuid} sub_processor PROCTHROTTLEMIN {minFrequency}")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (Process process = Process.Start(psi))
                    {
                        if (process != null)
                        {
                            process.WaitForExit();

                            if (process.ExitCode != 0)
                            {
                                string error = process.StandardError.ReadToEnd();
                                MessageBox.Show($"Error setting frequency: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }

                            psi = new ProcessStartInfo("powercfg", $"/setactive {activePlanGuid}")
                            {
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            using (Process applyProcess = Process.Start(psi))
                            {
                                if (applyProcess != null)
                                {
                                    applyProcess.WaitForExit();
                                    if (applyProcess.ExitCode == 0)
                                    {
                                        _powerManagementService.savedMinFrequency = minFrequency;
                                        _chartService.UpdateDvfsInfo();

                                        System.Threading.Thread.Sleep(500);

                                        MessageBox.Show($"CPU minimum frequency set to {minFrequency}%\n" +
                                                      "Changes may take a few seconds to take effect.",
                                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        MessageBox.Show("Cannot apply power plan changes", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Please enter a valid value (1-100)", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting CPU frequency: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetMaxCpuFrequency_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (int.TryParse(MaxFrequencyTextBox.Text, out int maxFrequency) && maxFrequency > 0 && maxFrequency <= 100)
                {
                    string activePlanGuid = GetCurrentActivePowerPlan();
                    if (string.IsNullOrEmpty(activePlanGuid))
                    {
                        MessageBox.Show("Cannot get current power plan information", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    ProcessStartInfo psi = new ProcessStartInfo("powercfg",
                        $"/setacvalueindex {activePlanGuid} sub_processor PROCTHROTTLEMAX {maxFrequency}")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (Process process = Process.Start(psi))
                    {
                        if (process != null)
                        {
                            process.WaitForExit();

                            if (process.ExitCode != 0)
                            {
                                string error = process.StandardError.ReadToEnd();
                                MessageBox.Show($"Error setting frequency: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }

                            psi = new ProcessStartInfo("powercfg", $"/setactive {activePlanGuid}")
                            {
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            using (Process applyProcess = Process.Start(psi))
                            {
                                if (applyProcess != null)
                                {
                                    applyProcess.WaitForExit();
                                    if (applyProcess.ExitCode == 0)
                                    {
                                        _powerManagementService.savedMaxFrequency = maxFrequency;
                                        _chartService.UpdateDvfsInfo();

                                        System.Threading.Thread.Sleep(500);

                                        MessageBox.Show($"CPU maximum frequency set to {maxFrequency}%\n" +
                                                      "Changes may take a few seconds to take effect.",
                                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        MessageBox.Show("Cannot apply power plan changes", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Please enter a valid value (1-100)", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting CPU frequency: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyScreenOffTime_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(ScreenOffMinutesTextBox.Text, out int minutes) && minutes >= 0)
            {
                try
                {
                    var psi = new ProcessStartInfo("powercfg", $"/change monitor-timeout-ac {minutes}")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi)?.WaitForExit();
                    MessageBox.Show($"Screen turn-off time updated: {minutes} minutes", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error setting screen turn-off time: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please enter a valid number of minutes.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ApplySleepTime_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(SleepMinutesTextBox.Text, out int minutes) && minutes >= 0)
            {
                try
                {
                    var psi = new ProcessStartInfo("powercfg", $"/change standby-timeout-ac {minutes}")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi)?.WaitForExit();
                    MessageBox.Show($"Sleep time updated: {minutes} minutes", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error setting sleep time: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please enter a valid number of minutes.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RefreshProcessList_Click(object sender, RoutedEventArgs e)
        {
            _processService.RefreshProcessList();
        }

        private void StartProcessMonitor_Click(object sender, RoutedEventArgs e)
        {
            _processService.StartProcessMonitoring();
        }

        private void StopProcessMonitor_Click(object sender, RoutedEventArgs e)
        {
            _processService.StopProcessMonitoring();
        }

        private void EndProcess_ContextMenu_Click(object sender, RoutedEventArgs e)
        {
            // Get selected item in ListView
            if (ProcessListView.SelectedItem != null)
            {
                // Assume ProcessListView.SelectedItem has Id property
                var selectedProcess = ProcessListView.SelectedItem;
                var processIdProperty = selectedProcess.GetType().GetProperty("Id");

                if (processIdProperty != null)
                {
                    int processId = (int)processIdProperty.GetValue(selectedProcess);
                    var processNameProperty = selectedProcess.GetType().GetProperty("Name");
                    string processName = processNameProperty?.GetValue(selectedProcess)?.ToString() ?? "Unknown";

                    var result = MessageBox.Show(
                        $"Are you sure you want to end process '{processName}' (ID: {processId})?",
                        "Confirm End Process",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        _processService.KillProcess(processId);
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a process to end.", "Notice",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ProcessListView_ColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            ListSortDirection direction;

            if (headerClicked != null)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
                    if (headerClicked != _lastHeaderClicked)
                    {
                        direction = ListSortDirection.Ascending;
                    }
                    else
                    {
                        if (_lastDirection == ListSortDirection.Ascending)
                        {
                            direction = ListSortDirection.Descending;
                        }
                        else
                        {
                            direction = ListSortDirection.Ascending;
                        }
                    }

                    var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
                    var sortBy = columnBinding?.Path.Path ?? headerClicked.Column.Header as string;

                    Sort(sortBy, direction);

                    if (direction == ListSortDirection.Ascending)
                    {
                        headerClicked.Column.HeaderTemplate = Application.Current.Resources["HeaderTemplateArrowUp"] as DataTemplate;
                    }
                    else
                    {
                        headerClicked.Column.HeaderTemplate = Application.Current.Resources["HeaderTemplateArrowDown"] as DataTemplate;
                    }

                    // Remove arrow from previously sorted header
                    if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked)
                    {
                        _lastHeaderClicked.Column.HeaderTemplate = null;
                    }

                    _lastHeaderClicked = headerClicked;
                    _lastDirection = direction;
                }
            }
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView = CollectionViewSource.GetDefaultView(ProcessListView.ItemsSource);

            dataView.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }

        private void LoadDefaultWarningSettings()
        {
            CpuThresholdTextBox.Text = _warningService.CpuWarningThreshold.ToString();
            CpuTempThresholdTextBox.Text = _warningService.CpuTemperatureThreshold.ToString();
            RamThresholdTextBox.Text = _warningService.RamWarningThreshold.ToString();
            GpuThresholdTextBox.Text = _warningService.GpuWarningThreshold.ToString();
            GpuTempThresholdTextBox.Text = _warningService.GpuTemperatureThreshold.ToString();
            DiskThresholdTextBox.Text = _warningService.DiskWarningThreshold.ToString();
        }

        protected override void OnClosed(EventArgs e)
        {
            _processService?.StopProcessMonitoring();
            _notifyIcon?.Dispose();
            _backgroundTimer?.Stop();
            base.OnClosed(e);
            cancellationTokenSource?.Cancel();
            autoAdjustCancellationTokenSource?.Cancel();
            _monitoringService.computer?.Close();

        }

        private void SetResourceLimit_ContextMenu_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessListView.SelectedItem != null)
            {
                var selectedProcess = ProcessListView.SelectedItem;
                var processIdProperty = selectedProcess.GetType().GetProperty("Id");

                if (processIdProperty != null)
                {
                    int processId = (int)processIdProperty.GetValue(selectedProcess);
                    var processNameProperty = selectedProcess.GetType().GetProperty("Name");
                    string processName = processNameProperty?.GetValue(selectedProcess)?.ToString() ?? "Unknown";

                    // Tạo dialog để nhập giới hạn tài nguyên
                    ShowResourceLimitDialog(processId, processName);
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một process để thiết lập giới hạn!", "Thông báo",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RemoveResourceLimit_ContextMenu_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessListView.SelectedItem != null)
            {
                var selectedProcess = ProcessListView.SelectedItem;
                var processIdProperty = selectedProcess.GetType().GetProperty("Id");

                if (processIdProperty != null)
                {
                    int processId = (int)processIdProperty.GetValue(selectedProcess);
                    var processNameProperty = selectedProcess.GetType().GetProperty("Name");
                    string processName = processNameProperty?.GetValue(selectedProcess)?.ToString() ?? "Unknown";

                    var result = MessageBox.Show(
                        $"Bạn có chắc muốn bỏ giới hạn tài nguyên cho process '{processName}' (ID: {processId})?",
                        "Xác nhận",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        _processService.RemoveProcessResourceLimit(processId);
                    }
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một process để bỏ giới hạn!", "Thông báo",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ViewResourceLimits_Click(object sender, RoutedEventArgs e)
        {
            var limits = _processService.GetProcessLimits();

            if (limits.Count == 0)
            {
                MessageBox.Show("Hiện tại không có process nào được thiết lập giới hạn tài nguyên.",
                               "Thông tin", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string message = "Danh sách process có giới hạn tài nguyên:\n\n";
            foreach (var limit in limits)
            {
                message += $"• {limit.ProcessName} (ID: {limit.ProcessId})\n";
                message += $"  RAM: {limit.MemoryLimitMB}MB, CPU: {limit.CpuLimitPercent}%\n";
                message += $"  Thời gian tạo: {limit.CreatedTime:HH:mm:ss dd/MM/yyyy}\n\n";
            }

            MessageBox.Show(message, "Giới hạn tài nguyên", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Method để hiển thị dialog thiết lập giới hạn
        private void ShowResourceLimitDialog(int processId, string processName)
        {
            // Tạo window dialog đơn giản
            Window dialog = new Window()
            {
                Title = $"Thiết lập giới hạn tài nguyên - {processName}",
                Width = 400,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            StackPanel mainPanel = new StackPanel() { Margin = new Thickness(20) };

            // Memory limit
            Label memoryLabel = new Label() { Content = "Giới hạn RAM (MB):" };
            TextBox memoryTextBox = new TextBox() { Name = "MemoryLimit", Margin = new Thickness(0, 0, 0, 10) };

            // CPU limit
            Label cpuLabel = new Label() { Content = "Giới hạn CPU (%):" };
            TextBox cpuTextBox = new TextBox() { Name = "CpuLimit", Margin = new Thickness(0, 0, 0, 20) };

            // Buttons
            StackPanel buttonPanel = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            Button okButton = new Button() { Content = "OK", Width = 80, Height = 30, Margin = new Thickness(0, 0, 10, 0) };
            Button cancelButton = new Button() { Content = "Hủy", Width = 80, Height = 30 };

            okButton.Click += (s, e) =>
            {
                try
                {
                    int memoryLimit = 0;
                    int cpuLimit = 0;

                    if (!string.IsNullOrWhiteSpace(memoryTextBox.Text))
                    {
                        if (!int.TryParse(memoryTextBox.Text, out memoryLimit) || memoryLimit <= 0)
                        {
                            MessageBox.Show("Vui lòng nhập giá trị RAM hợp lệ (số nguyên dương)!", "Lỗi",
                                           MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(cpuTextBox.Text))
                    {
                        if (!int.TryParse(cpuTextBox.Text, out cpuLimit) || cpuLimit <= 0 || cpuLimit > 100)
                        {
                            MessageBox.Show("Vui lòng nhập giá trị CPU hợp lệ (1-100)!", "Lỗi",
                                           MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    if (memoryLimit == 0 && cpuLimit == 0)
                    {
                        MessageBox.Show("Vui lòng nhập ít nhất một giới hạn (RAM hoặc CPU)!", "Lỗi",
                                       MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    bool success = _processService.SetProcessResourceLimit(processId, memoryLimit, cpuLimit);
                    if (success)
                    {
                        dialog.DialogResult = true;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            cancelButton.Click += (s, e) => { dialog.DialogResult = false; };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            mainPanel.Children.Add(memoryLabel);
            mainPanel.Children.Add(memoryTextBox);
            mainPanel.Children.Add(cpuLabel);
            mainPanel.Children.Add(cpuTextBox);
            mainPanel.Children.Add(buttonPanel);

            dialog.Content = mainPanel;

            dialog.ShowDialog();
        }
    }

    public class PowerPlan
    {
        public string Guid { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }

        public override string ToString()
        {
            return IsActive ? $"{Name} (Active)" : Name;
        }
    }

    public class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this);
        }

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware)
            {
                subHardware.Accept(this);
            }
        }

        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}
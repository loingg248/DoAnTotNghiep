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
using static SystemMonitor.Services.WarningService;
using System.Windows.Input;


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

        private ScrollViewer _solutionsPanel;
        private ItemsControl _solutionsList;
        private Button _showSolutionsButton;
        private List<SolutionRecommendation> _currentSolutions;


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

            _warningService.SolutionsUpdated += OnSolutionsUpdated;
            _warningService.SolutionsVisibilityChanged += OnSolutionsVisibilityChanged;

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

            // Tự động bắt đầu theo dõi sau khi khởi tạo
            AutoStartMonitoring();

            _solutionsPanel = FindName("SolutionsPanel") as ScrollViewer;
            _solutionsList = FindName("SolutionsList") as ItemsControl;
            _showSolutionsButton = FindName("ShowSolutionsButton") as Button;
        }

        private void InitializeBackgroundMode()
        {
            // Lấy NotifyIcon từ resources
            _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");

            // Tạo timer cho chế độ chạy ngầm
            _backgroundTimer = new DispatcherTimer();
            _backgroundTimer.Interval = TimeSpan.FromSeconds(30); // 30 giây
            _backgroundTimer.Tick += BackgroundTimer_Tick;
        }

        private void InitializeServices()
        {
            // Cấu hình warning service cho chế độ nền
            _warningService.WarningTriggered += OnBackgroundWarning;

            // Cấu hình UI controls cho warning service
            _warningService.SetUIControls(WarningStatus, WarningBorder);
        }


        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!_isReallyClosing)
            {
                // Ngăn đóng app
                e.Cancel = true;

                // Hiển thị dialog hỏi người dùng
                var result = MessageBox.Show(
                    "Bạn có muốn thu nhỏ ứng dụng xuống khay hệ thống thay vì thoát không?\n\n" +
                    "- Chọn 'Có' để thu nhỏ xuống khay hệ thống\n" +
                    "- Chọn 'Không' để thoát ứng dụng hoàn toàn",
                    "Giám Sát Hệ Thống",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Ẩn window và hiển thị tray icon
                    Hide();
                    _notifyIcon.Visibility = Visibility.Visible;

                    // Hiển thị thông báo
                    _notifyIcon.ShowBalloonTip("Giám Sát Hệ Thống",
                        "Ứng dụng đã được thu nhỏ xuống khay hệ thống. Việc giám sát tiếp tục chạy ngầm.",
                        BalloonIcon.Info);

                    // Chuyển sang chế độ chạy ngầm
                    StartBackgroundMode();
                }
                else if (result == MessageBoxResult.No)
                {
                    // Thoát ứng dụng hoàn toàn
                    _isReallyClosing = true;
                    _notifyIcon?.Dispose();
                    Application.Current.Shutdown();
                }
                // Nếu chọn Cancel thì không làm gì cả (giữ nguyên cửa sổ)
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
                System.Diagnostics.Debug.WriteLine($"Lỗi giám sát ngầm: {ex.Message}");
            }
        }

        private void OnBackgroundWarning(object sender, string warningMessage)
        {
            // Hiển thị cảnh báo qua tray notification
            Dispatcher.Invoke(() =>
            {
                _notifyIcon.ShowBalloonTip("⚠️ Cảnh Báo Hệ Thống",
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
            // Chuyển đến tab Cảnh báo
            if (sender is TabControl tabControl)
            {
                tabControl.SelectedIndex = 2; // Index của tab Cảnh báo
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

                    // Cập nhật UI để phản ánh trạng thái giám sát
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tự động bắt đầu giám sát: {ex.Message}", "Lỗi",
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
                MessageBox.Show($"Lỗi khi lấy thông tin kế hoạch nguồn hiện tại: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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

                WarningSettingsStatus.Text = $"✅ Cài đặt đã lưu: CPU {cpuThreshold}%, RAM {ramThreshold}%, GPU {gpuThreshold}%";
                WarningSettingsStatus.Foreground = new SolidColorBrush(Colors.Green);

                MessageBox.Show("Cài đặt cảnh báo đã được lưu thành công!", "Thành công",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WarningSettingsStatus.Text = "❌ Lỗi: Vui lòng nhập số hợp lệ";
                WarningSettingsStatus.Foreground = new SolidColorBrush(Colors.Red);

                MessageBox.Show($"Lỗi khi lưu cài đặt: {ex.Message}", "Lỗi",
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

                // Cập nhật UI nếu cần
                // StartMonitorButton.Content = "⏹ Dừng Giám Sát";
            }
        }

        private void StopMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            if (isMonitoring)
            {
                isMonitoring = false;
                cancellationTokenSource?.Cancel();

                // Cập nhật UI nếu cần
                // StartMonitorButton.Content = "▶ Bắt Đầu Giám Sát";
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
                AutoAdjustCpuButton.Content = "⏹ Dừng Điều Chỉnh Tự Động";
                autoAdjustCancellationTokenSource = new CancellationTokenSource();
                _powerManagementService.StartAutoAdjustCpu(autoAdjustCancellationTokenSource.Token);
            }
            else
            {
                isAutoAdjustEnabled = false;
                AutoAdjustCpuButton.Content = "🤖 Bắt Đầu Điều Chỉnh Tự Động";
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
                        MessageBox.Show("Không thể lấy thông tin kế hoạch nguồn hiện tại", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                                MessageBox.Show($"Lỗi khi đặt tần số: {error}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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

                                        MessageBox.Show($"Tần số CPU tối thiểu đã được đặt thành {minFrequency}%\n" +
                                                      "Thay đổi có thể mất vài giây để có hiệu lực.",
                                            "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        MessageBox.Show("Không thể áp dụng thay đổi kế hoạch nguồn", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Vui lòng nhập giá trị hợp lệ (1-100)", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đặt tần số CPU: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        MessageBox.Show("Không thể lấy thông tin kế hoạch nguồn hiện tại", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                                MessageBox.Show($"Lỗi khi đặt tần số: {error}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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

                                        MessageBox.Show($"Tần số CPU tối đa đã được đặt thành {maxFrequency}%\n" +
                                                      "Thay đổi có thể mất vài giây để có hiệu lực.",
                                            "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        MessageBox.Show("Không thể áp dụng thay đổi kế hoạch nguồn", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Vui lòng nhập giá trị hợp lệ (1-100)", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đặt tần số CPU: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show($"Thời gian tắt màn hình đã được cập nhật: {minutes} phút", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi đặt thời gian tắt màn hình: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Vui lòng nhập số phút hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show($"Thời gian chờ đã được cập nhật: {minutes} phút", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi đặt thời gian chờ: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Vui lòng nhập số phút hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            // Lấy item được chọn trong ListView
            if (ProcessListView.SelectedItem != null)
            {
                // Giả sử ProcessListView.SelectedItem có thuộc tính Id
                var selectedProcess = ProcessListView.SelectedItem;
                var processIdProperty = selectedProcess.GetType().GetProperty("Id");

                if (processIdProperty != null)
                {
                    int processId = (int)processIdProperty.GetValue(selectedProcess);
                    var processNameProperty = selectedProcess.GetType().GetProperty("Name");
                    string processName = processNameProperty?.GetValue(selectedProcess)?.ToString() ?? "Không xác định";

                    var result = MessageBox.Show(
                        $"Bạn có chắc chắn muốn kết thúc tiến trình '{processName}' (ID: {processId})?",
                        "Xác nhận kết thúc tiến trình",
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
                MessageBox.Show("Vui lòng chọn một tiến trình để kết thúc.", "Thông báo",
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

                    // Xóa mũi tên từ header đã sắp xếp trước đó
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
                    string processName = processNameProperty?.GetValue(selectedProcess)?.ToString() ?? "Không rõ";

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
                    string processName = processNameProperty?.GetValue(selectedProcess)?.ToString() ?? "Không rõ";

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
            }

            MessageBox.Show(message, "Giới hạn tài nguyên", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Phương thức để hiển thị dialog thiết lập giới hạn
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

            // Giới hạn bộ nhớ
            Label memoryLabel = new Label() { Content = "Giới hạn RAM (MB):" };
            TextBox memoryTextBox = new TextBox() { Name = "MemoryLimit", Margin = new Thickness(0, 0, 0, 10) };

            // Giới hạn CPU
            Label cpuLabel = new Label() { Content = "Giới hạn CPU (%):" };
            TextBox cpuTextBox = new TextBox() { Name = "CpuLimit", Margin = new Thickness(0, 0, 0, 20) };

            // Các nút
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

        private void ShowSolutions_Click(object sender, RoutedEventArgs e)
        {
            // Ẩn các phần cài đặt cảnh báo
            HeaderSection.Visibility = Visibility.Collapsed;
            SettingsGrid.Visibility = Visibility.Collapsed;
            StatusSection.Visibility = Visibility.Collapsed;
            ShowSolutionsButton.Visibility = Visibility.Collapsed;

            // Hiển thị phần giải pháp
            SolutionsHeader.Visibility = Visibility.Visible;
            SolutionsPanel.Visibility = Visibility.Visible;
            HideSolutionsButton.Visibility = Visibility.Visible;

            // Kiểm tra và gán dữ liệu cho SolutionsList nếu có
            if (_currentSolutions?.Any() == true)
            {
                SolutionsList.ItemsSource = _currentSolutions;
            }
        }

        private void HideSolutions_Click(object sender, RoutedEventArgs e)
        {
            // Hiển thị lại các phần cài đặt cảnh báo
            HeaderSection.Visibility = Visibility.Visible;
            SettingsGrid.Visibility = Visibility.Visible;
            StatusSection.Visibility = Visibility.Visible;
            ShowSolutionsButton.Visibility = Visibility.Visible;

            // Ẩn phần giải pháp
            SolutionsHeader.Visibility = Visibility.Collapsed;
            SolutionsPanel.Visibility = Visibility.Collapsed;
            HideSolutionsButton.Visibility = Visibility.Collapsed;
        }

        private void Solution_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            var solution = border?.DataContext as WarningService.SolutionRecommendation;

            if (solution != null)
            {
                HandleSolutionAction(solution);
            }
        }

        private void HandleSolutionAction(WarningService.SolutionRecommendation solution)
        {
            try
            {
                switch (solution.ActionType)
                {
                    case "TAB_SWITCH":
                        HandleTabSwitchAction(solution.ActionData);
                        break;
                    case "PROCESS_ACTION":
                        HandleProcessAction(solution.ActionData);
                        break;
                    case "EXTERNAL":
                        HandleExternalAction(solution.ActionData);
                        break;
                    default:
                        MessageBox.Show(solution.Action, "Hướng dẫn thực hiện",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thực hiện: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HandleTabSwitchAction(string actionData)
        {
            var parts = actionData.Split('|');
            var tabName = parts[0];

            // Tìm TabControl
            var tabControl = FindName("MainTabControl") as TabControl;
            if (tabControl == null)
            {
                // Nếu không có tên, tìm theo loại
                tabControl = FindChildOfType<TabControl>(this);
            }

            if (tabControl != null)
            {
                switch (tabName)
                {
                    case "PowerSettings":
                        // Chuyển sang tab Cài đặt nguồn điện (index 1)
                        tabControl.SelectedIndex = 1;

                        if (parts.Length > 1)
                        {
                            ExecutePowerAction(parts[1], parts.Length > 2 ? parts[2] : null);
                        }
                        break;

                    case "ProcessMonitor":
                        // Chuyển sang tab Giám sát tiến trình (index 3)
                        tabControl.SelectedIndex = 3;

                        if (parts.Length > 1)
                        {
                            ExecuteProcessMonitorAction(parts[1]);
                        }
                        break;

                    case "Alerts":
                        // Chuyển sang tab Cảnh báo (index 2)
                        tabControl.SelectedIndex = 2;
                        break;
                }
            }
        }

        private void ExecutePowerAction(string action, string value)
        {
            switch (action)
            {
                case "EnableSmartMode":
                    // Kích hoạt nút Chế độ thông minh
                    var smartButton = FindName("AutoAdjustCpuButton") as Button;
                    if (smartButton != null && smartButton.Content.ToString().Contains("Bật"))
                    {
                        smartButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        ShowActionResult("✅ Đã bật Chế độ thông minh tự động điều chỉnh CPU");
                    }
                    break;

                case "SetMaxCPU":
                    // Thiết lập tần số CPU tối đa
                    var maxFreqTextBox = FindName("MaxFrequencyTextBox") as TextBox;
                    var setMaxButton = FindName("SetMaxCpuFrequency_Click") as Button;

                    if (maxFreqTextBox != null && value != null)
                    {
                        maxFreqTextBox.Text = value;
                        // Kích hoạt nút thiết lập nếu tồn tại
                        ShowActionResult($"✅ Đã thiết lập tần số CPU tối đa = {value}%\nNhấn nút Thiết lập để áp dụng");
                    }
                    break;

                case "SetPowerPlan":
                    var powerPlanCombo = FindName("PowerPlanComboBox") as ComboBox;
                    if (powerPlanCombo != null && value != null)
                    {
                        // Tìm và chọn gói nguồn điện phù hợp
                        for (int i = 0; i < powerPlanCombo.Items.Count; i++)
                        {
                            if (powerPlanCombo.Items[i].ToString().Contains(value))
                            {
                                powerPlanCombo.SelectedIndex = i;
                                ShowActionResult($"✅ Đã chuyển sang gói nguồn điện {value}");
                                break;
                            }
                        }
                    }
                    break;
            }
        }

        private void ExecuteProcessMonitorAction(string action)
        {
            switch (action)
            {
                case "SortByCPU":
                    var processListView = FindName("ProcessListView") as ListView;
                    if (processListView != null)
                    {
                        // Làm mới danh sách tiến trình trước
                        RefreshProcessList_Click(null, null);
                        ShowActionResult("✅ Đã làm mới Giám sát tiến trình\nCác tiến trình được sắp xếp theo mức sử dụng CPU");
                    }
                    break;

                case "SortByMemory":
                    var processListView2 = FindName("ProcessListView") as ListView;
                    if (processListView2 != null)
                    {
                        RefreshProcessList_Click(null, null);
                        ShowActionResult("✅ Đã làm mới Giám sát tiến trình\nKiểm tra cột Bộ nhớ để tìm tiến trình tốn RAM");
                    }
                    break;
            }
        }

        private void HandleProcessAction(string actionData)
        {
            // Xử lý các hành động liên quan đến tiến trình (kết thúc tiến trình, thiết lập giới hạn, v.v.)
            var parts = actionData.Split('|');
            var action = parts[0];

            switch (action)
            {
                case "EndHighCpuProcess":
                    ShowActionResult("💡 Hướng dẫn:\n" +
                        "1. Chuyển sang tab Giám sát tiến trình\n" +
                        "2. Tìm tiến trình có CPU% cao nhất\n" +
                        "3. Nhấp chuột phải → Kết thúc tiến trình\n" +
                        "⚠️ Cẩn thận với các tiến trình hệ thống!");
                    break;

                case "SetResourceLimit":
                    ShowActionResult("💡 Hướng dẫn thiết lập giới hạn tài nguyên:\n" +
                        "1. Chuyển sang tab Giám sát tiến trình\n" +
                        "2. Nhấp chuột phải vào tiến trình cần giới hạn\n" +
                        "3. Chọn 'Thiết lập giới hạn tài nguyên'\n" +
                        "4. Đặt giới hạn CPU/Bộ nhớ phù hợp");
                    break;
            }
        }

        private void HandleExternalAction(string actionData)
        {
            // Xử lý các hành động bên ngoài (mở ứng dụng, cài đặt, v.v.)
            ShowActionResult($"💡 Thực hiện thủ công:\n{actionData}");
        }

        private void ShowActionResult(string message)
        {
            MessageBox.Show(message, "Thực hiện thành công",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Phương thức trợ giúp để tìm control con theo loại
        private T FindChildOfType<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T result)
                    return result;

                var foundChild = FindChildOfType<T>(child);
                if (foundChild != null)
                    return foundChild;
            }

            return null;
        }

        private void QuickAction_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var solution = button?.Tag as WarningService.SolutionRecommendation;

            if (solution != null)
            {
                ExecuteQuickAction(solution);
            }
        }

        private void ShowGuide_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var solution = button?.Tag as WarningService.SolutionRecommendation;

            if (solution != null)
            {
                ShowDetailedGuide(solution);
            }
        }

        private void ExecuteQuickAction(WarningService.SolutionRecommendation solution)
        {
            try
            {
                switch (solution.Title)
                {
                    case "Bật Smart Mode":
                        ExecuteSmartModeAction();
                        break;

                    case "Chuyển Power Plan":
                        ExecutePowerPlanAction();
                        break;

                    case "Giảm CPU Frequency":
                        ExecuteCpuFrequencyAction();
                        break;

                    case "Quản lý Process":
                        ExecuteProcessManagementAction();
                        break;

                    case "Giới hạn tài nguyên":
                        ExecuteResourceLimitAction();
                        break;

                    case "Tìm Process tốn RAM":
                    case "Tìm Process tốn CPU":
                        ExecuteProcessAnalysisAction();
                        break;

                    default:
                        // Dự phòng: hiển thị hướng dẫn
                        ShowDetailedGuide(solution);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thực hiện: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteSmartModeAction()
        {
            // Chuyển sang tab Cài đặt nguồn điện
            var tabControl = FindChildOfType<TabControl>(this);
            if (tabControl != null)
            {
                tabControl.SelectedIndex = 1; // Tab Cài đặt nguồn điện

                // Tìm và nhấp nút Chế độ thông minh
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var smartButton = FindName("AutoAdjustCpuButton") as Button;
                    if (smartButton != null)
                    {
                        if (smartButton.Content.ToString().Contains("Bật"))
                        {
                            smartButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            ShowQuickActionResult("✅ Đã bật Chế độ thông minh!",
                                "Hệ thống sẽ tự động điều chỉnh tần số CPU theo mức độ sử dụng.");
                        }
                        else
                        {
                            ShowQuickActionResult("ℹ️ Chế độ thông minh đã được bật",
                                "Chế độ thông minh hiện đang hoạt động và tự động điều chỉnh CPU.");
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void ExecutePowerPlanAction()
        {
            var tabControl = FindChildOfType<TabControl>(this);
            if (tabControl != null)
            {
                tabControl.SelectedIndex = 1; // Tab Cài đặt nguồn điện

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var powerPlanCombo = FindName("PowerPlanComboBox") as ComboBox;
                    if (powerPlanCombo != null)
                    {
                        // Tìm gói Tiết kiệm năng lượng hoặc Cân bằng
                        for (int i = 0; i < powerPlanCombo.Items.Count; i++)
                        {
                            var item = powerPlanCombo.Items[i].ToString();
                            if (item.Contains("Tiết kiệm năng lượng") || item.Contains("Cân bằng") ||
                                item.Contains("Power saver") || item.Contains("Balanced"))
                            {
                                powerPlanCombo.SelectedIndex = i;
                                ShowQuickActionResult("✅ Đã chuyển gói nguồn điện!",
                                    $"Chuyển sang: {item}");
                                return;
                            }
                        }
                        ShowQuickActionResult("ℹ️ Không tìm thấy gói nguồn điện phù hợp",
                            "Vui lòng chọn Tiết kiệm năng lượng hoặc Cân bằng thủ công.");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void ExecuteCpuFrequencyAction()
        {
            var tabControl = FindChildOfType<TabControl>(this);
            if (tabControl != null)
            {
                tabControl.SelectedIndex = 1; // Tab Cài đặt nguồn điện

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var maxFreqTextBox = FindName("MaxFrequencyTextBox") as TextBox;
                    if (maxFreqTextBox != null)
                    {
                        maxFreqTextBox.Text = "70"; // Thiết lập về 70%
                        ShowQuickActionResult("✅ Đã thiết lập tần số CPU tối đa!",
                            "Đã đặt tần số CPU tối đa = 70%. Nhấn nút 'Thiết lập' để áp dụng thay đổi.");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        // Tiếp tục các event handlers cho các nút giải pháp

        private void ExecuteProcessManagementAction()
        {
            var tabControl = FindChildOfType<TabControl>(this);
            if (tabControl != null)
            {
                tabControl.SelectedIndex = 3; // Tab Giám sát tiến trình

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Làm mới danh sách tiến trình trước
                    RefreshProcessList_Click(null, null);

                    ShowQuickActionResult("✅ Đã chuyển sang Giám sát tiến trình!",
                        "Danh sách tiến trình đã được làm mới. Tìm tiến trình có CPU% cao nhất để đóng.");
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void ExecuteResourceLimitAction()
        {
            var tabControl = FindChildOfType<TabControl>(this);
            if (tabControl != null)
            {
                tabControl.SelectedIndex = 3; // Tab Giám sát tiến trình

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RefreshProcessList_Click(null, null);

                    ShowQuickActionResult("✅ Đã mở Giám sát tiến trình!",
                        "Nhấp chuột phải vào tiến trình cần giới hạn → Chọn 'Thiết lập giới hạn tài nguyên'");
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void ExecuteProcessAnalysisAction()
        {
            var tabControl = FindChildOfType<TabControl>(this);
            if (tabControl != null)
            {
                tabControl.SelectedIndex = 3; // Tab Giám sát tiến trình

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RefreshProcessList_Click(null, null);

                    // Phân tích và hiển thị các tiến trình hàng đầu
                    AnalyzeTopProcesses();

                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void AnalyzeTopProcesses()
        {
            try
            {
                var processListView = FindName("ProcessListView") as ListView;
                if (processListView?.ItemsSource != null)
                {
                    var processes = processListView.ItemsSource as IEnumerable<ProcessInfo>;
                    if (processes != null)
                    {
                        var topCpuProcesses = processes
                            .Where(p => p.CpuUsage > 5) // Chỉ lấy tiến trình > 5% CPU
                            .OrderByDescending(p => p.CpuUsage)
                            .Take(3)
                            .ToList();

                        var topMemoryProcesses = processes
                            .Where(p => p.MemoryUsage > 100) // Chỉ lấy tiến trình > 100MB RAM
                            .OrderByDescending(p => p.MemoryUsage)
                            .Take(3)
                            .ToList();

                        string analysisResult = "📊 Phân tích Tiến trình:\n\n";

                        if (topCpuProcesses.Any())
                        {
                            analysisResult += "🔥 Tiến trình CPU Cao Nhất:\n";
                            foreach (var proc in topCpuProcesses)
                            {
                                analysisResult += $"  • {proc.Name}: {proc.CpuUsage:F1}%\n";
                            }
                            analysisResult += "\n";
                        }

                        if (topMemoryProcesses.Any())
                        {
                            analysisResult += "💾 Tiến trình Bộ nhớ Cao Nhất:\n";
                            foreach (var proc in topMemoryProcesses)
                            {
                                analysisResult += $"  • {proc.Name}: {proc.MemoryUsage:F0} MB\n";
                            }
                        }

                        ShowQuickActionResult("✅ Phân tích hoàn tất!", analysisResult);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowQuickActionResult("⚠️ Lỗi phân tích", $"Không thể phân tích tiến trình: {ex.Message}");
            }
        }

        private void ShowDetailedGuide(WarningService.SolutionRecommendation solution)
        {
            var guideWindow = new SolutionGuideWindow(solution);
            guideWindow.Owner = this;
            guideWindow.ShowDialog();
        }

        private void ShowQuickActionResult(string title, string message)
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnSolutionsUpdated(List<SolutionRecommendation> solutions)
        {
            _currentSolutions = solutions;

            // Cập nhật giao diện danh sách giải pháp
            if (_solutionsList != null)
            {
                _solutionsList.ItemsSource = solutions;
            }

            // Hiển thị nút thao tác nhanh
            CreateQuickActionButtons(solutions);
        }

        private void OnSolutionsVisibilityChanged(bool isVisible)
        {
            if (_solutionsPanel != null)
            {
                _solutionsPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_showSolutionsButton != null)
            {
                _showSolutionsButton.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void CreateQuickActionButtons(List<SolutionRecommendation> solutions)
        {
            // Tìm container cho nút thao tác nhanh
            var quickActionsPanel = FindName("QuickActionsPanel") as StackPanel;

            if (quickActionsPanel != null)
            {
                quickActionsPanel.Children.Clear();

                // Tạo nút thao tác nhanh cho 3-4 giải pháp hàng đầu
                foreach (var solution in solutions.Take(4))
                {
                    var button = new Button
                    {
                        Content = $"{solution.Icon} {solution.Title}",
                        Tag = solution,
                        Margin = new Thickness(5, 2, 5, 2),
                        Padding = new Thickness(10, 5, 10, 5),
                        Background = new SolidColorBrush(Color.FromRgb(0, 123, 255)),
                        Foreground = Brushes.White,
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0, 86, 179)),
                        BorderThickness = new Thickness(1),
                        Cursor = Cursors.Hand
                    };

                    button.Click += QuickAction_Click;
                    quickActionsPanel.Children.Add(button);
                }

                quickActionsPanel.Visibility = Visibility.Visible;
            }
        }
    }
}


public class PowerPlan
{
    public string Guid { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }

    public override string ToString()
    {
        return IsActive ? $"{Name} (Đang hoạt động)" : Name;
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


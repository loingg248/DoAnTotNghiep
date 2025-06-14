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


namespace SystemMonitor
{
    public partial class MainWindow : Window
    {
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

        private readonly StartupService _startupService;


        public MainWindow()
        {
            InitializeComponent();

            _monitoringService = new MonitoringService();
            _powerManagementService = new PowerManagementService();
            _chartService = new ChartService(CpuChart, RamChart, GpuChart, DiskChart);
            _warningService = new WarningService();
            _warningService.SetUIControls(WarningStatus, WarningBorder);
            _processService = new ProcessService();
            _processService.SetUIControls(ProcessListView, ProcessCountLabel);

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

            // Tự động bắt đầu monitoring khi khởi tạo xong
            AutoStartMonitoring();

            _startupService = new StartupService();
            _startupService.SetUIControls(StartupListView, StartupCountLabel);
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

                    // Cập nhật UI để phản ánh trạng thái monitoring
                    // Giả sử bạn có button để bắt đầu/dừng monitoring
                    // StartMonitorButton.Content = "⏹ Dừng giám sát";
                    // StartMonitorButton.IsEnabled = true;
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
                MessageBox.Show($"Lỗi khi lấy power plan hiện tại: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return string.Empty;
        }

        private void UpdateUI(object sender, SystemInfoEventArgs e)
        {
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

                WarningSettingsStatus.Text = $"✅ Cài đặt đã được lưu: CPU {cpuThreshold}%, RAM {ramThreshold}%, GPU {gpuThreshold}%";
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
                // StartMonitorButton.Content = "⏹ Dừng giám sát";
            }
        }

        private void StopMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            if (isMonitoring)
            {
                isMonitoring = false;
                cancellationTokenSource?.Cancel();

                // Cập nhật UI nếu cần
                // StartMonitorButton.Content = "▶ Bắt đầu giám sát";
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
                AutoAdjustCpuButton.Content = "⏹ Dừng điều chỉnh tự động";
                autoAdjustCancellationTokenSource = new CancellationTokenSource();
                _powerManagementService.StartAutoAdjustCpu(autoAdjustCancellationTokenSource.Token);
            }
            else
            {
                isAutoAdjustEnabled = false;
                AutoAdjustCpuButton.Content = "🤖 Bắt đầu điều chỉnh tự động";
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
                        MessageBox.Show("Không thể lấy thông tin power plan hiện tại", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                                MessageBox.Show($"Lỗi khi thiết lập tần số: {error}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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

                                        MessageBox.Show($"Đã đặt tần số tối thiểu CPU thành {minFrequency}%\n" +
                                                      "Thay đổi có thể mất vài giây để có hiệu lực.",
                                            "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        MessageBox.Show("Không thể áp dụng thay đổi power plan", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Lỗi khi thiết lập tần số CPU: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        MessageBox.Show("Không thể lấy thông tin power plan hiện tại", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                                MessageBox.Show($"Lỗi khi thiết lập tần số: {error}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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

                                        MessageBox.Show($"Đã đặt tần số tối đa CPU thành {maxFrequency}%\n" +
                                                      "Thay đổi có thể mất vài giây để có hiệu lực.",
                                            "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        MessageBox.Show("Không thể áp dụng thay đổi power plan", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Lỗi khi thiết lập tần số CPU: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show($"Đã cập nhật thời gian tắt màn hình: {minutes} phút", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    MessageBox.Show($"Đã cập nhật thời gian Sleep: {minutes} phút", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi đặt thời gian Sleep: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                // Giả sử ProcessListView.SelectedItem có property Id
                var selectedProcess = ProcessListView.SelectedItem;
                var processIdProperty = selectedProcess.GetType().GetProperty("Id");

                if (processIdProperty != null)
                {
                    int processId = (int)processIdProperty.GetValue(selectedProcess);
                    var processNameProperty = selectedProcess.GetType().GetProperty("Name");
                    string processName = processNameProperty?.GetValue(selectedProcess)?.ToString() ?? "Unknown";

                    var result = MessageBox.Show(
                        $"Bạn có chắc chắn muốn kết thúc process '{processName}' (ID: {processId})?",
                        "Xác nhận kết thúc process",
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
                MessageBox.Show("Vui lòng chọn một process để kết thúc.", "Thông báo",
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

        private void RefreshStartupList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _startupService.LoadStartupItems();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải danh sách startup: {ex.Message}", "Lỗi",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisableStartupItem_Click(object sender, RoutedEventArgs e)
        {
            if (StartupListView.SelectedItem is StartupItem selectedItem)
            {
                if (selectedItem.Status == "Enabled")
                {
                    var result = MessageBox.Show(
                        $"Bạn có chắc chắn muốn vô hiệu hóa startup item '{selectedItem.Name}'?",
                        "Xác nhận",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        _startupService.DisableStartupItem(selectedItem);
                    }
                }
                else
                {
                    MessageBox.Show("Item này đã được vô hiệu hóa rồi.", "Thông báo",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một startup item để vô hiệu hóa.", "Thông báo",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void EnableStartupItem_Click(object sender, RoutedEventArgs e)
        {
            if (StartupListView.SelectedItem is StartupItem selectedItem)
            {
                if (selectedItem.Status == "Disabled")
                {
                    _startupService.EnableStartupItem(selectedItem);
                }
                else
                {
                    MessageBox.Show("Item này đang được kích hoạt.", "Thông báo",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một startup item để kích hoạt.", "Thông báo",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OpenFileLocation_ContextMenu_Click(object sender, RoutedEventArgs e)
        {
            if (StartupListView.SelectedItem is StartupItem selectedItem)
            {
                _startupService.OpenFileLocation(selectedItem);
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một startup item.", "Thông báo",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DisableStartupItem_ContextMenu_Click(object sender, RoutedEventArgs e)
        {
            DisableStartupItem_Click(sender, e);
        }

        private void EnableStartupItem_ContextMenu_Click(object sender, RoutedEventArgs e)
        {
            EnableStartupItem_Click(sender, e);
        }

        private void StartupListView_ColumnHeaderClick(object sender, RoutedEventArgs e)
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

                    SortStartup(sortBy, direction);

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

        private void SortStartup(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView = CollectionViewSource.GetDefaultView(StartupListView.ItemsSource);

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
            _startupService.LoadStartupItems();
        }

        protected override void OnClosed(EventArgs e)
        {
            _processService?.StopProcessMonitoring();
            base.OnClosed(e);
            cancellationTokenSource?.Cancel();
            autoAdjustCancellationTokenSource?.Cancel();
            _monitoringService.computer?.Close();
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
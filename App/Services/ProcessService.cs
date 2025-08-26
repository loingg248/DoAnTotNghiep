using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SystemMonitor.Services
{
    public class ProcessService
    {
        private readonly ResourceLimitService _resourceLimitService;

        private ListView _processListView;
        private TextBlock _processCountLabel;

        private ObservableCollection<ProcessInfo> _displayedProcesses;
        private Dictionary<int, ProcessInfo> _allProcesses;

        private Timer _processTimer;
        private readonly object _lockObject = new object();

        // Để theo dõi CPU usage
        private Dictionary<int, ProcessCpuInfo> _previousProcessInfo;
        private DateTime _lastUpdateTime;

        // Cache cho icons
        private Dictionary<string, ImageSource> _iconCache;

        // Thông tin hệ thống
        private readonly int _processorCount;
        private PerformanceCounter _totalCpuCounter;

        // Cấu hình cập nhật
        private const int MAX_DISPLAYED_PROCESSES = 50;
        private const int UPDATE_INTERVAL_MS = 1000; // Giảm xuống 1s để chính xác hơn
        private const double CPU_CHANGE_THRESHOLD = 0.5; // Giảm threshold
        private const long MEMORY_CHANGE_THRESHOLD = 1; // Giảm threshold RAM

        public ProcessService()
        {
            _displayedProcesses = new ObservableCollection<ProcessInfo>();
            _allProcesses = new Dictionary<int, ProcessInfo>();
            _previousProcessInfo = new Dictionary<int, ProcessCpuInfo>();
            _iconCache = new Dictionary<string, ImageSource>();
            _lastUpdateTime = DateTime.Now;

            _resourceLimitService = new ResourceLimitService();

            // Lấy số core CPU
            _processorCount = Environment.ProcessorCount;

            // Khởi tạo counter cho tổng CPU
            try
            {
                _totalCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _totalCpuCounter.NextValue(); // First call để khởi tạo
            }
            catch
            {
                _totalCpuCounter = null;
            }
        }

        public void SetUIControls(ListView processListView, TextBlock processCountLabel)
        {
            _processListView = processListView;
            _processCountLabel = processCountLabel;
            _processListView.ItemsSource = _displayedProcesses;
        }

        public void StartProcessMonitoring()
        {
            StopProcessMonitoring();
            RefreshProcessList();

            _processTimer = new Timer(UpdateProcessList, null,
                TimeSpan.FromMilliseconds(UPDATE_INTERVAL_MS),
                TimeSpan.FromMilliseconds(UPDATE_INTERVAL_MS));
        }

        public void StopProcessMonitoring()
        {
            _processTimer?.Dispose();
            _processTimer = null;
            _resourceLimitService?.Stop();
            _totalCpuCounter?.Dispose();
        }

        public void RefreshProcessList()
        {
            try
            {
                var currentTime = DateTime.Now;

                // Thực hiện việc lấy process data trên background thread
                Task.Run(() =>
                {
                    try
                    {
                        var processData = new List<(int Id, string Name, long Memory, double Cpu, string Status, ProcessPriorityClass Priority, Process Process)>();

                        foreach (var process in Process.GetProcesses().Where(p => !string.IsNullOrEmpty(p.ProcessName)))
                        {
                            try
                            {
                                var id = process.Id;
                                var name = process.ProcessName;
                                var memory = GetProcessMemoryUsage(process);
                                var cpu = CalculateCpuUsage(process, currentTime);
                                var status = GetProcessStatus(process);
                                var priority = GetProcessPriority(process);

                                processData.Add((id, name, memory, cpu, status, priority, process));
                            }
                            catch
                            {
                                continue;
                            }
                        }

                        // Tạo UI objects trên UI thread
                        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                var newProcesses = new Dictionary<int, ProcessInfo>();

                                foreach (var data in processData)
                                {
                                    var processInfo = new ProcessInfo
                                    {
                                        Id = data.Id,
                                        Name = data.Name,
                                        MemoryUsage = data.Memory,
                                        CpuUsage = data.Cpu,
                                        Status = data.Status,
                                        Priority = data.Priority,
                                        Icon = GetProcessIcon(data.Process)
                                    };

                                    newProcesses[data.Id] = processInfo;
                                }

                                lock (_lockObject)
                                {
                                    _allProcesses = newProcesses;
                                }

                                _lastUpdateTime = currentTime;
                                UpdateDisplayedProcesses(isFullRefresh: true);
                                UpdateProcessCountLabel();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Lỗi khi cập nhật UI: {ex.Message}", "Lỗi",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }));
                    }
                    catch (Exception ex)
                    {
                        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            MessageBox.Show($"Lỗi khi lấy danh sách process: {ex.Message}", "Lỗi",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }));
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khởi tạo: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateProcessList(object state)
        {
            try
            {
                var currentTime = DateTime.Now;
                var newProcesses = new Dictionary<int, ProcessInfo>();
                var changedProcesses = new List<ProcessInfo>();

                // Tạo processes trên background thread
                var processData = new List<(int Id, string Name, long Memory, double Cpu, string Status, ProcessPriorityClass Priority, Process Process)>();

                foreach (var process in Process.GetProcesses().Where(p => !string.IsNullOrEmpty(p.ProcessName)))
                {
                    try
                    {
                        var id = process.Id;
                        var name = process.ProcessName;
                        var memory = GetProcessMemoryUsage(process);
                        var cpu = CalculateCpuUsage(process, currentTime);
                        var status = GetProcessStatus(process);
                        var priority = GetProcessPriority(process);

                        processData.Add((id, name, memory, cpu, status, priority, process));
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Tạo ProcessInfo objects trên UI thread
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        foreach (var data in processData)
                        {
                            var processInfo = new ProcessInfo
                            {
                                Id = data.Id,
                                Name = data.Name,
                                MemoryUsage = data.Memory,
                                CpuUsage = data.Cpu,
                                Status = data.Status,
                                Priority = data.Priority,
                                Icon = GetProcessIcon(data.Process)
                            };

                            newProcesses[data.Id] = processInfo;

                            if (ShouldUpdateProcess(processInfo))
                            {
                                changedProcesses.Add(processInfo);
                            }
                        }

                        lock (_lockObject)
                        {
                            var removedProcessIds = _allProcesses.Keys.Except(newProcesses.Keys).ToList();
                            _allProcesses = newProcesses;

                            if (changedProcesses.Any() || removedProcessIds.Any())
                            {
                                UpdateDisplayedProcesses(isFullRefresh: false, changedProcesses, removedProcessIds);
                            }
                        }

                        _lastUpdateTime = currentTime;
                        CleanupOldProcesses();
                    }
                    catch (Exception)
                    {
                        // Bỏ qua lỗi trong quá trình cập nhật
                    }
                }));
            }
            catch (Exception)
            {
                // Bỏ qua lỗi trong quá trình cập nhật định kỳ
            }
        }

        private ProcessInfo CreateProcessInfo(Process process, DateTime currentTime)
        {
            var processInfo = new ProcessInfo
            {
                Id = process.Id,
                Name = process.ProcessName,
                MemoryUsage = GetProcessMemoryUsage(process),
                CpuUsage = CalculateCpuUsage(process, currentTime),
                Status = GetProcessStatus(process),
                Priority = GetProcessPriority(process)
            };

            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    processInfo.Icon = GetProcessIcon(process);
                }
                catch
                {
                    processInfo.Icon = null;
                }
            });

            return processInfo;
        }

        private bool ShouldUpdateProcess(ProcessInfo newProcessInfo)
        {
            if (!_allProcesses.ContainsKey(newProcessInfo.Id))
            {
                return true; // Process mới
            }

            var oldProcessInfo = _allProcesses[newProcessInfo.Id];

            // Kiểm tra thay đổi CPU
            if (Math.Abs(newProcessInfo.CpuUsage - oldProcessInfo.CpuUsage) > CPU_CHANGE_THRESHOLD)
            {
                return true;
            }

            // Kiểm tra thay đổi Memory
            if (Math.Abs(newProcessInfo.MemoryUsage - oldProcessInfo.MemoryUsage) > MEMORY_CHANGE_THRESHOLD)
            {
                return true;
            }

            // Kiểm tra thay đổi Status
            if (newProcessInfo.Status != oldProcessInfo.Status)
            {
                return true;
            }

            // Kiểm tra thay đổi Priority
            if (newProcessInfo.Priority != oldProcessInfo.Priority)
            {
                return true;
            }

            return false;
        }

        private void UpdateDisplayedProcesses(bool isFullRefresh,
            List<ProcessInfo> changedProcesses = null,
            List<int> removedProcessIds = null)
        {
            try
            {
                if (isFullRefresh)
                {
                    var topProcesses = _allProcesses.Values
                        .OrderByDescending(p => p.CpuUsage)
                        .Take(MAX_DISPLAYED_PROCESSES)
                        .ToList();

                    _displayedProcesses.Clear();
                    foreach (var process in topProcesses)
                    {
                        _displayedProcesses.Add(process);
                    }
                }
                else
                {
                    // Xóa các process đã kết thúc
                    if (removedProcessIds?.Any() == true)
                    {
                        for (int i = _displayedProcesses.Count - 1; i >= 0; i--)
                        {
                            if (removedProcessIds.Contains(_displayedProcesses[i].Id))
                            {
                                _displayedProcesses.RemoveAt(i);
                            }
                        }
                    }

                    // Cập nhật hoặc thêm process đã thay đổi
                    if (changedProcesses?.Any() == true)
                    {
                        foreach (var changedProcess in changedProcesses)
                        {
                            var existingIndex = -1;
                            for (int i = 0; i < _displayedProcesses.Count; i++)
                            {
                                if (_displayedProcesses[i].Id == changedProcess.Id)
                                {
                                    existingIndex = i;
                                    break;
                                }
                            }

                            if (existingIndex >= 0)
                            {
                                var existing = _displayedProcesses[existingIndex];
                                existing.CpuUsage = changedProcess.CpuUsage;
                                existing.MemoryUsage = changedProcess.MemoryUsage;
                                existing.Status = changedProcess.Status;
                                existing.Priority = changedProcess.Priority;
                            }
                            else if (_displayedProcesses.Count < MAX_DISPLAYED_PROCESSES)
                            {
                                _displayedProcesses.Add(changedProcess);
                            }
                            else
                            {
                                var lowestCpuProcess = _displayedProcesses.OrderBy(p => p.CpuUsage).FirstOrDefault();
                                if (lowestCpuProcess != null && changedProcess.CpuUsage > lowestCpuProcess.CpuUsage)
                                {
                                    _displayedProcesses.Remove(lowestCpuProcess);
                                    _displayedProcesses.Add(changedProcess);
                                }
                            }
                        }

                        // Sắp xếp lại mỗi 10 giây
                        if (DateTime.Now.Second % 10 == 0)
                        {
                            var sortedList = _displayedProcesses.OrderByDescending(p => p.CpuUsage).ToList();
                            _displayedProcesses.Clear();
                            foreach (var process in sortedList)
                            {
                                _displayedProcesses.Add(process);
                            }
                        }
                    }
                }

                UpdateProcessCountLabel();
            }
            catch (Exception)
            {
            }
        }

        private void UpdateProcessCountLabel()
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    _processCountLabel.Text = $"Tổng số process: {_allProcesses.Count} (Hiển thị top {Math.Min(_displayedProcesses.Count, MAX_DISPLAYED_PROCESSES)})";
                }
                catch (Exception)
                {
                    // Bỏ qua lỗi UI
                }
            }));
        }

        private double CalculateCpuUsage(Process process, DateTime currentTime)
        {
            try
            {
                var currentCpuTime = process.TotalProcessorTime;
                var processId = process.Id;

                if (_previousProcessInfo.ContainsKey(processId))
                {
                    var prevInfo = _previousProcessInfo[processId];
                    var cpuTimeDiff = (currentCpuTime - prevInfo.CpuTime).TotalMilliseconds;
                    var realTimeDiff = (currentTime - prevInfo.Timestamp).TotalMilliseconds;

                    if (realTimeDiff > 0)
                    {
                        var cpuUsage = (cpuTimeDiff / realTimeDiff) * 100.0 / _processorCount;

                        _previousProcessInfo[processId] = new ProcessCpuInfo
                        {
                            CpuTime = currentCpuTime,
                            Timestamp = currentTime
                        };

                        return Math.Max(0, Math.Min(cpuUsage, 100.0));
                    }
                }

                _previousProcessInfo[processId] = new ProcessCpuInfo
                {
                    CpuTime = currentCpuTime,
                    Timestamp = currentTime
                };

                return 0.0;
            }
            catch
            {
                return 0.0;
            }
        }

        private long GetProcessMemoryUsage(Process process)
        {
            try
            {
                long workingSet = process.WorkingSet64 / (1024 * 1024); // Convert to MB
                return workingSet;
            }
            catch
            {
                return 0;
            }
        }

        private string GetProcessStatus(Process process)
        {
            try
            {
                if (process.HasExited)
                    return "Đã thoát";

                try
                {
                    foreach (ProcessThread thread in process.Threads)
                    {
                        if (thread.ThreadState == System.Diagnostics.ThreadState.Wait &&
                            thread.WaitReason == ThreadWaitReason.Suspended)
                        {
                            return "Tạm dừng";
                        }
                    }
                }
                catch
                {
                    // Không thể kiểm tra threads
                }

                return process.Responding ? "Đang chạy" : "Không phản hồi";
            }
            catch
            {
                return "Không xác định";
            }
        }

        private ProcessPriorityClass GetProcessPriority(Process process)
        {
            try
            {
                return process.PriorityClass;
            }
            catch
            {
                return ProcessPriorityClass.Normal; // Giá trị mặc định nếu không lấy được
            }
        }

        private ImageSource GetProcessIcon(Process process)
        {
            try
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    return Application.Current.Dispatcher.Invoke(() => GetProcessIcon(process));
                }

                string cacheKey = process.ProcessName.ToLower();
                if (_iconCache.ContainsKey(cacheKey))
                {
                    return _iconCache[cacheKey];
                }

                ImageSource iconSource = null;

                try
                {
                    string fileName = process.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
                    {
                        using (var icon = Icon.ExtractAssociatedIcon(fileName))
                        {
                            if (icon != null)
                            {
                                iconSource = Imaging.CreateBitmapSourceFromHIcon(
                                    icon.Handle,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());

                                iconSource?.Freeze();
                            }
                        }
                    }
                }
                catch
                {
                }

                if (iconSource == null)
                {
                    iconSource = GetDefaultIconForProcess(process.ProcessName);
                }

                if (iconSource != null)
                {
                    _iconCache[cacheKey] = iconSource;
                }

                return iconSource;
            }
            catch
            {
                return GetDefaultIconForProcess(process.ProcessName);
            }
        }

        private ImageSource GetDefaultIconForProcess(string processName)
        {
            string iconText = "⚙️";
            string lowerProcessName = processName.ToLower();

            if (lowerProcessName.Contains("chrome") || lowerProcessName.Contains("firefox") ||
                lowerProcessName.Contains("edge") || lowerProcessName.Contains("browser"))
            {
                iconText = "🌐";
            }
            else if (lowerProcessName.Contains("explorer"))
            {
                iconText = "📁";
            }
            else if (lowerProcessName.Contains("notepad") || lowerProcessName.Contains("word") ||
                     lowerProcessName.Contains("excel") || lowerProcessName.Contains("powerpnt"))
            {
                iconText = "📝";
            }
            else if (lowerProcessName.Contains("steam") || lowerProcessName.Contains("game") ||
                     lowerProcessName.Contains("unity"))
            {
                iconText = "🎮";
            }
            else if (lowerProcessName.Contains("music") || lowerProcessName.Contains("spotify") ||
                     lowerProcessName.Contains("vlc") || lowerProcessName.Contains("media"))
            {
                iconText = "🎵";
            }
            else if (lowerProcessName.Contains("visual") || lowerProcessName.Contains("code") ||
                     lowerProcessName.Contains("dev"))
            {
                iconText = "💻";
            }
            else if (lowerProcessName.Contains("system") || lowerProcessName.Contains("service"))
            {
                iconText = "🔧";
            }
            else if (lowerProcessName.Contains("antivirus") || lowerProcessName.Contains("defender"))
            {
                iconText = "🛡️";
            }

            return CreateTextIcon(iconText);
        }

        private ImageSource CreateTextIcon(string text)
        {
            try
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    return Application.Current.Dispatcher.Invoke(() => CreateTextIcon(text));
                }

                int width = 16, height = 16;
                var bitmap = new System.Drawing.Bitmap(width, height);

                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(System.Drawing.Color.Transparent);

                    using (var font = new Font("Segoe UI Emoji", 10))
                    using (var brush = new SolidBrush(System.Drawing.Color.Black))
                    {
                        var stringFormat = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        };

                        graphics.DrawString(text, font, brush,
                            new RectangleF(0, 0, width, height), stringFormat);
                    }
                }

                var hBitmap = bitmap.GetHbitmap();
                var imageSource = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                imageSource?.Freeze();

                DeleteObject(hBitmap);
                bitmap.Dispose();

                return imageSource;
            }
            catch
            {
                return null;
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public void KillProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(5000);

                    if (_previousProcessInfo.ContainsKey(processId))
                    {
                        _previousProcessInfo.Remove(processId);
                    }

                    Task.Delay(1000).ContinueWith(_ => RefreshProcessList());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể kết thúc process: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SetProcessPriority(int processId, ProcessPriorityClass priority)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process == null || process.HasExited)
                {
                    MessageBox.Show("Tiến trình không tồn tại hoặc đã kết thúc!", "Lỗi",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                process.PriorityClass = priority;
                RefreshProcessList(); // Cập nhật danh sách để phản ánh thay đổi
                MessageBox.Show($"Đã đặt ưu tiên cho {process.ProcessName} thành {priority}.", "Thành công",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đặt ưu tiên: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CleanupOldProcesses()
        {
            var currentProcessIds = _allProcesses.Keys.ToHashSet();
            var keysToRemove = _previousProcessInfo.Keys.Where(id => !currentProcessIds.Contains(id)).ToList();

            foreach (var key in keysToRemove)
            {
                _previousProcessInfo.Remove(key);
            }
        }

        public bool SetProcessResourceLimit(int processId, int memoryLimitMB, int cpuLimitPercent)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process == null || process.HasExited)
                {
                    MessageBox.Show("Process không tồn tại hoặc đã kết thúc!", "Lỗi",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                var limit = new ProcessResourceLimit
                {
                    ProcessId = processId,
                    ProcessName = process.ProcessName,
                    MemoryLimitMB = memoryLimitMB,
                    CpuLimitPercent = cpuLimitPercent
                };

                bool success = _resourceLimitService.SetProcessResourceLimit(processId, limit);

                if (success)
                {
                    MessageBox.Show($"Đã thiết lập giới hạn tài nguyên cho {process.ProcessName}:\n" +
                                    $"RAM: {memoryLimitMB}MB\n" +
                                    $"CPU: {cpuLimitPercent}%",
                                    "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Không thể thiết lập giới hạn tài nguyên!", "Lỗi",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return success;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool RemoveProcessResourceLimit(int processId)
        {
            try
            {
                bool success = _resourceLimitService.RemoveProcessResourceLimit(processId);

                if (success)
                {
                    MessageBox.Show("Đã bỏ giới hạn tài nguyên!", "Thành công",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return success;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public List<ProcessResourceLimit> GetProcessLimits()
        {
            return _resourceLimitService.GetProcessLimits();
        }
    }

    public class ProcessCpuInfo
    {
        public TimeSpan CpuTime { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ProcessInfo : System.ComponentModel.INotifyPropertyChanged
    {
        private int _id;
        private string _name;
        private long _memoryUsage;
        private double _cpuUsage;
        private string _status;
        private ImageSource _icon;
        private ProcessPriorityClass _priority;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public long MemoryUsage
        {
            get => _memoryUsage;
            set { _memoryUsage = value; OnPropertyChanged(); OnPropertyChanged(nameof(MemoryUsageText)); }
        }

        public double CpuUsage
        {
            get => _cpuUsage;
            set { _cpuUsage = value; OnPropertyChanged(); OnPropertyChanged(nameof(CpuUsageText)); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public ImageSource Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        public ProcessPriorityClass Priority
        {
            get => _priority;
            set { _priority = value; OnPropertyChanged(); OnPropertyChanged(nameof(PriorityText)); }
        }

        public string PriorityText
        {
            get
            {
                return _priority switch
                {
                    ProcessPriorityClass.RealTime => "Thời gian thực",
                    ProcessPriorityClass.High => "Cao",
                    ProcessPriorityClass.AboveNormal => "Trên trung bình",
                    ProcessPriorityClass.Normal => "Bình thường",
                    ProcessPriorityClass.BelowNormal => "Dưới trung bình",
                    ProcessPriorityClass.Idle => "Rảnh rỗi",
                    _ => "Không xác định"
                };
            }
        }

        public string MemoryUsageText => $"{MemoryUsage} MB";
        public string CpuUsageText => $"{CpuUsage:F1}%";

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}

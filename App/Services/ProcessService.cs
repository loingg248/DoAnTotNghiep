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
        private ListView _processListView;
        private TextBlock _processCountLabel;

        // Thay đổi từ List sang ObservableCollection để tránh refresh toàn bộ
        private ObservableCollection<ProcessInfo> _displayedProcesses;
        private Dictionary<int, ProcessInfo> _allProcesses;

        private Timer _processTimer;
        private readonly object _lockObject = new object();

        // Để theo dõi CPU usage
        private Dictionary<int, ProcessCpuInfo> _previousProcessInfo;
        private DateTime _lastUpdateTime;

        // Cache cho icons
        private Dictionary<string, ImageSource> _iconCache;

        // Cấu hình cập nhật
        private const int MAX_DISPLAYED_PROCESSES = 50;
        private const int UPDATE_INTERVAL_MS = 3000; // Tăng từ 2s lên 3s
        private const double CPU_CHANGE_THRESHOLD = 1.0; // Chỉ cập nhật khi CPU thay đổi > 1%
        private const long MEMORY_CHANGE_THRESHOLD = 5; // Chỉ cập nhật khi RAM thay đổi > 5MB

        public ProcessService()
        {
            _displayedProcesses = new ObservableCollection<ProcessInfo>();
            _allProcesses = new Dictionary<int, ProcessInfo>();
            _previousProcessInfo = new Dictionary<int, ProcessCpuInfo>();
            _iconCache = new Dictionary<string, ImageSource>();
            _lastUpdateTime = DateTime.Now;
        }

        public void SetUIControls(ListView processListView, TextBlock processCountLabel)
        {
            _processListView = processListView;
            _processCountLabel = processCountLabel;

            // Set ItemsSource một lần duy nhất
            _processListView.ItemsSource = _displayedProcesses;
        }

        public void StartProcessMonitoring()
        {
            StopProcessMonitoring();
            RefreshProcessList();

            // Cập nhật với interval dài hơn
            _processTimer = new Timer(UpdateProcessList, null,
                TimeSpan.FromMilliseconds(UPDATE_INTERVAL_MS),
                TimeSpan.FromMilliseconds(UPDATE_INTERVAL_MS));
        }

        public void StopProcessMonitoring()
        {
            _processTimer?.Dispose();
            _processTimer = null;
        }

        public void RefreshProcessList()
        {
            try
            {
                var currentTime = DateTime.Now;
                var newProcesses = new Dictionary<int, ProcessInfo>();

                foreach (var process in Process.GetProcesses().Where(p => !string.IsNullOrEmpty(p.ProcessName)))
                {
                    try
                    {
                        var processInfo = CreateProcessInfo(process, currentTime);
                        newProcesses[process.Id] = processInfo;
                    }
                    catch
                    {
                        continue;
                    }
                }

                lock (_lockObject)
                {
                    _allProcesses = newProcesses;
                    UpdateDisplayedProcesses(isFullRefresh: true);
                }

                _lastUpdateTime = currentTime;
                UpdateProcessCountLabel();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Lỗi khi lấy danh sách process: {ex.Message}", "Lỗi",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void UpdateProcessList(object state)
        {
            try
            {
                var currentTime = DateTime.Now;
                var newProcesses = new Dictionary<int, ProcessInfo>();
                var changedProcesses = new List<ProcessInfo>();

                foreach (var process in Process.GetProcesses().Where(p => !string.IsNullOrEmpty(p.ProcessName)))
                {
                    try
                    {
                        var processInfo = CreateProcessInfo(process, currentTime);
                        newProcesses[process.Id] = processInfo;

                        // Kiểm tra xem process có thay đổi đáng kể không
                        if (ShouldUpdateProcess(processInfo))
                        {
                            changedProcesses.Add(processInfo);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                lock (_lockObject)
                {
                    // Cập nhật dictionary chính
                    var removedProcessIds = _allProcesses.Keys.Except(newProcesses.Keys).ToList();
                    _allProcesses = newProcesses;

                    // Chỉ cập nhật UI nếu có thay đổi đáng kể
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
                // Bỏ qua lỗi trong quá trình cập nhật định kỳ
            }
        }

        private ProcessInfo CreateProcessInfo(Process process, DateTime currentTime)
        {
            return new ProcessInfo
            {
                Id = process.Id,
                Name = process.ProcessName,
                MemoryUsage = GetProcessMemoryUsage(process),
                CpuUsage = CalculateCpuUsage(process, currentTime),
                Status = GetProcessStatus(process),
                Icon = GetProcessIcon(process)
            };
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

            return false;
        }

        private void UpdateDisplayedProcesses(bool isFullRefresh,
            List<ProcessInfo> changedProcesses = null,
            List<int> removedProcessIds = null)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (isFullRefresh)
                    {
                        // Full refresh - chỉ khi cần thiết (như lần đầu load)
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
                        // Incremental update

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
                                    // Cập nhật process hiện có
                                    var existing = _displayedProcesses[existingIndex];
                                    existing.CpuUsage = changedProcess.CpuUsage;
                                    existing.MemoryUsage = changedProcess.MemoryUsage;
                                    existing.Status = changedProcess.Status;
                                    // Không cần cập nhật Icon và Name vì chúng ít thay đổi
                                }
                                else if (_displayedProcesses.Count < MAX_DISPLAYED_PROCESSES)
                                {
                                    // Thêm process mới nếu còn chỗ
                                    _displayedProcesses.Add(changedProcess);
                                }
                                else
                                {
                                    // Kiểm tra xem process mới có CPU cao hơn process thấp nhất không
                                    var lowestCpuProcess = _displayedProcesses.OrderBy(p => p.CpuUsage).FirstOrDefault();
                                    if (lowestCpuProcess != null && changedProcess.CpuUsage > lowestCpuProcess.CpuUsage)
                                    {
                                        _displayedProcesses.Remove(lowestCpuProcess);
                                        _displayedProcesses.Add(changedProcess);
                                    }
                                }
                            }

                            // Sắp xếp lại chỉ khi cần thiết (ví dụ: mỗi 10 giây)
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
                    // Bỏ qua lỗi UI
                }
            }));
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

        // Các method khác giữ nguyên
        private ImageSource GetProcessIcon(Process process)
        {
            try
            {
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
                            }
                        }
                    }
                }
                catch
                {
                    // Không thể lấy icon từ file
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
                        var cpuUsage = (cpuTimeDiff / realTimeDiff) * 100.0;

                        _previousProcessInfo[processId] = new ProcessCpuInfo
                        {
                            CpuTime = currentCpuTime,
                            Timestamp = currentTime
                        };

                        return Math.Min(cpuUsage, 100.0);
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
                return process.WorkingSet64 / (1024 * 1024);
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

                return process.Responding ? "Đang chạy" : "Không phản hồi";
            }
            catch
            {
                return "Không xác định";
            }
        }

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

                    // Refresh sau khi kill
                    Task.Delay(1000).ContinueWith(_ => RefreshProcessList());
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Không thể kết thúc process: {ex.Message}", "Lỗi",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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

        public string MemoryUsageText => $"{MemoryUsage} MB";
        public string CpuUsageText => $"{CpuUsage:F1}%";

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
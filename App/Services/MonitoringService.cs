using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using LibreHardwareMonitor.Hardware;

namespace SystemMonitor.Services
{
    public class SystemInfoEventArgs : EventArgs
    {
        public float CpuUsage { get; set; }
        public float CpuTemperature { get; set; }
        public float RamUsage { get; set; }
        public float GpuUsage { get; set; }
        public float DiskUsage { get; set; }
        public string CpuName { get; set; }
        public float CpuClock { get; set; }
        public float RamUsed { get; set; }
        public float RamAvailable { get; set; }
        public float RamTotal { get; set; }
        public string GpuName { get; set; }
        public float GpuTemperature { get; set; }
        public float GpuMemoryUsed { get; set; }
        public float GpuMemoryTotal { get; set; }
        public string DiskName { get; set; }
        public float DiskTemperature { get; set; }
        public float DiskActivity { get; set; }
        public float DiskUsed { get; set; }
        public float DiskTotal { get; set; }
    }

    public class MonitoringService
    {
        public event EventHandler<SystemInfoEventArgs> DataUpdated;

        public Computer? computer;
        public PerformanceCounter? cpuUsageCounter;
        private PerformanceCounter? availableMemoryCounter;
        private PerformanceCounter? gpuUsageCounter;
        private PerformanceCounter? diskUsageCounter;

        // Biến để theo dõi GPU ưu tiên
        private IHardware? priorityGpu = null;

        public TextBlock CpuName { get; set; }
        public TextBlock CpuTemp { get; set; }
        public TextBlock CpuLoad { get; set; }
        public TextBlock CpuClock { get; set; }
        public TextBlock RamUsage { get; set; }
        public TextBlock RamUsed { get; set; }
        public TextBlock RamAvailable { get; set; }
        public TextBlock RamTotal { get; set; }
        public TextBlock GpuName { get; set; }
        public TextBlock GpuTemp { get; set; }
        public TextBlock GpuLoad { get; set; }
        public TextBlock GpuMemory { get; set; }
        public TextBlock DiskName { get; set; }
        public TextBlock DiskActivity { get; set; }
        public TextBlock DiskTemp { get; set; }
        public TextBlock DiskSpace { get; set; }

        public MonitoringService()
        {
            InitializeComputer();
        }

        public void SetUIControls(
            TextBlock cpuName, TextBlock cpuTemp, TextBlock cpuLoad, TextBlock cpuClock,
            TextBlock ramUsage, TextBlock ramUsed, TextBlock ramAvailable, TextBlock ramTotal,
            TextBlock gpuName, TextBlock gpuTemp, TextBlock gpuLoad, TextBlock gpuMemory,
            TextBlock diskName, TextBlock diskActivity, TextBlock diskTemp, TextBlock diskSpace)
        {
            CpuName = cpuName;
            CpuTemp = cpuTemp;
            CpuLoad = cpuLoad;
            CpuClock = cpuClock;
            RamUsage = ramUsage;
            RamUsed = ramUsed;
            RamAvailable = ramAvailable;
            RamTotal = ramTotal;
            GpuName = gpuName;
            GpuTemp = gpuTemp;
            GpuLoad = gpuLoad;
            GpuMemory = gpuMemory;
            DiskName = diskName;
            DiskActivity = diskActivity;
            DiskTemp = diskTemp;
            DiskSpace = diskSpace;
        }

        private void InitializeComputer()
        {
            computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true
            };
            computer.Open();
            cpuUsageCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            availableMemoryCounter = new PerformanceCounter("Memory", "Available MBytes");

            try
            {
                diskUsageCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
            }
            catch (Exception)
            {
            }

            // Khởi tạo GPU ưu tiên
            InitializePriorityGpu();

            try
            {
                var categories = PerformanceCounterCategory.GetCategories()
                    .Where(c => c.CategoryName.Contains("GPU") || c.CategoryName.Contains("NVIDIA") || c.CategoryName.Contains("AMD"));

                foreach (var category in categories)
                {
                    try
                    {
                        var instances = category.GetInstanceNames();
                        if (instances.Length > 0)
                        {
                            var counters = category.GetCounters(instances[0]);
                            foreach (var counter in counters)
                            {
                                if (counter.CounterName.Contains("Utilization") || counter.CounterName.Contains("Usage"))
                                {
                                    gpuUsageCounter = new PerformanceCounter(category.CategoryName, counter.CounterName, instances[0]);
                                    break;
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        // Phương thức mới để xác định GPU ưu tiên
        private void InitializePriorityGpu()
        {
            if (computer == null) return;

            // Danh sách ưu tiên GPU (rời -> tích hợp)
            var gpuPriorityOrder = new[]
            {
                HardwareType.GpuNvidia,  // GPU NVIDIA (thường là GPU rời)
                HardwareType.GpuAmd,     // GPU AMD (có thể là GPU rời)
                HardwareType.GpuIntel    // GPU Intel (thường là GPU tích hợp)
            };

            foreach (var gpuType in gpuPriorityOrder)
            {
                var gpu = computer.Hardware.FirstOrDefault(h => h.HardwareType == gpuType);
                if (gpu != null)
                {
                    // Kiểm tra thêm để đảm bảo ưu tiên GPU rời
                    if (IsDiscreteGpu(gpu))
                    {
                        priorityGpu = gpu;
                        Debug.WriteLine($"Đã chọn GPU rời ưu tiên: {gpu.Name}");
                        return;
                    }
                    else if (priorityGpu == null)
                    {
                        // Nếu chưa có GPU nào được chọn, chọn GPU này làm dự phòng
                        priorityGpu = gpu;
                        Debug.WriteLine($"Đã chọn GPU dự phòng: {gpu.Name}");
                    }
                }
            }

            if (priorityGpu != null)
            {
                Debug.WriteLine($"GPU cuối cùng được chọn: {priorityGpu.Name}");
            }
        }

        // Phương thức kiểm tra GPU có phải là GPU rời không
        private bool IsDiscreteGpu(IHardware gpu)
        {
            string name = gpu.Name.ToLower();

            // GPU rời thường có các từ khóa này
            string[] discreteKeywords = {
                "geforce", "rtx", "gtx", "radeon", "rx ", "r9", "r7", "r5",
                "titan", "quadro", "firepro", "vega", "fury"
            };

            // GPU tích hợp thường có các từ khóa này
            string[] integratedKeywords = {
                "intel", "uhd", "hd graphics", "iris", "integrated",
                "apu", "ryzen", "vega 3", "vega 5", "vega 6", "vega 7", "vega 8"
            };

            // Nếu chứa từ khóa GPU tích hợp, không phải GPU rời
            if (integratedKeywords.Any(keyword => name.Contains(keyword)))
            {
                return false;
            }

            // Nếu chứa từ khóa GPU rời, là GPU rời
            if (discreteKeywords.Any(keyword => name.Contains(keyword)))
            {
                return true;
            }

            // Nếu là NVIDIA hoặc AMD nhưng không có từ khóa tích hợp, có khả năng là GPU rời
            if (gpu.HardwareType == HardwareType.GpuNvidia ||
                (gpu.HardwareType == HardwareType.GpuAmd && !name.Contains("apu")))
            {
                return true;
            }

            return false;
        }

        public async void StartMonitoring(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    RefreshSystemInfo();
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
            }
        }

        public void RefreshSystemInfo()
        {
            if (computer == null) return;

            var systemInfo = new SystemInfoEventArgs();

            foreach (var hardware in computer.Hardware)
            {
                hardware.Update();

                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    systemInfo.CpuUsage = UpdateCpuInfo(hardware, systemInfo);
                }
                else if (hardware.HardwareType == HardwareType.Memory)
                {
                    systemInfo.RamUsage = UpdateMemoryInfo(hardware, systemInfo);
                }
                else if (hardware.HardwareType == HardwareType.Storage)
                {
                    systemInfo.DiskUsage = UpdateDiskInfo(hardware, systemInfo);
                }
            }

            // Cập nhật thông tin GPU ưu tiên
            if (priorityGpu != null)
            {
                priorityGpu.Update();
                systemInfo.GpuUsage = UpdateGpuInfo(priorityGpu, systemInfo);
            }
            else
            {
                // Nếu không tìm thấy GPU ưu tiên, tìm GPU đầu tiên có sẵn
                var anyGpu = computer.Hardware.FirstOrDefault(h =>
                    h.HardwareType == HardwareType.GpuNvidia ||
                    h.HardwareType == HardwareType.GpuAmd ||
                    h.HardwareType == HardwareType.GpuIntel);

                if (anyGpu != null)
                {
                    anyGpu.Update();
                    systemInfo.GpuUsage = UpdateGpuInfo(anyGpu, systemInfo);
                }
            }

            // Fallback cho GPU usage nếu không lấy được từ LibreHardwareMonitor
            if (systemInfo.GpuUsage == 0 && gpuUsageCounter != null)
            {
                try
                {
                    systemInfo.GpuUsage = gpuUsageCounter.NextValue();
                }
                catch
                {
                }
            }

            if (systemInfo.DiskUsage == 0 && diskUsageCounter != null)
            {
                try
                {
                    systemInfo.DiskUsage = diskUsageCounter.NextValue();
                    if (systemInfo.DiskUsage > 100) systemInfo.DiskUsage = 100;
                }
                catch
                {
                }
            }

            if (App.Current?.Dispatcher.CheckAccess() == true)
            {
                DataUpdated?.Invoke(this, systemInfo);
            }
            else
            {
                App.Current?.Dispatcher.Invoke(() => DataUpdated?.Invoke(this, systemInfo));
            }
        }

        public float UpdateCpuInfo(IHardware hardware, SystemInfoEventArgs systemInfo)
        {
            systemInfo.CpuName = hardware.Name;

            float temperature = 0;
            float usage = 0;
            float clock = 0;
            bool foundTemperature = false;

            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Temperature)
                {
                    if (!foundTemperature && (sensor.Name.Contains("CPU") || sensor.Name.Contains("Core")))
                    {
                        temperature = sensor.Value ?? 0;
                        foundTemperature = true;
                    }
                }
                else if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("CPU Total"))
                {
                    usage = sensor.Value ?? 0;
                }
                else if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("CPU Core"))
                {
                    if (clock == 0)
                    {
                        clock = sensor.Value ?? 0;
                    }
                }
            }

            if (!foundTemperature)
            {
                try
                {
                    using (var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            temperature = Convert.ToSingle(obj["CurrentTemperature"].ToString());
                            temperature = (temperature / 10) - 273.15f;
                            foundTemperature = true;
                            break;
                        }
                    }
                }
                catch
                {
                }
            }

            systemInfo.CpuTemperature = temperature;
            systemInfo.CpuClock = clock;

            App.Current?.Dispatcher.Invoke(() =>
            {
                if (CpuName != null) CpuName.Text = $"CPU Name: {hardware.Name}";
                if (CpuTemp != null) CpuTemp.Text = $"CPU Temperature: {temperature:F1}°C";
                if (CpuLoad != null) CpuLoad.Text = $"CPU Usage: {usage:F1}%";
                if (CpuClock != null) CpuClock.Text = $"CPU Clock: {clock:F0} MHz";
            });

            return usage;
        }

        private float UpdateMemoryInfo(IHardware hardware, SystemInfoEventArgs systemInfo)
        {
            float usedMemory = 0;
            float availableMemory = 0;
            float totalMemory = 0;
            float ramUsagePercent = 0;

            // Sử dụng WMI để lấy thông tin RAM chính xác ngay từ đầu
            try
            {
                // Lấy tổng RAM vật lý từ WMI
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        totalMemory = Convert.ToSingle(obj["TotalPhysicalMemory"]) / (1024f * 1024f * 1024f);
                        Debug.WriteLine($"WMI Total Physical Memory: {totalMemory:F2} GB");
                        break;
                    }
                }

                // Lấy RAM available từ Performance Counter
                if (availableMemoryCounter != null)
                {
                    availableMemory = availableMemoryCounter.NextValue() / 1024.0f; // Convert MB to GB
                    usedMemory = totalMemory - availableMemory;

                    if (totalMemory > 0)
                    {
                        ramUsagePercent = (usedMemory / totalMemory) * 100;
                    }

                    Debug.WriteLine($"Performance Counter - Available: {availableMemory:F2}GB, Used: {usedMemory:F2}GB, Usage: {ramUsagePercent:F1}%");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WMI/Performance Counter failed: {ex.Message}");

                // Fallback: Chỉ sử dụng sensor cuối cùng từ LibreHardwareMonitor
                var memoryUsedSensors = new List<ISensor>();
                var memoryAvailableSensors = new List<ISensor>();

                foreach (var sensor in hardware.Sensors)
                {
                    Debug.WriteLine($"Sensor: {sensor.Name}, Type: {sensor.SensorType}, Value: {sensor.Value}");

                    if (sensor.SensorType == SensorType.Data)
                    {
                        if (sensor.Name.Contains("Memory Used"))
                        {
                            memoryUsedSensors.Add(sensor);
                        }
                        else if (sensor.Name.Contains("Memory Available"))
                        {
                            memoryAvailableSensors.Add(sensor);
                        }
                    }
                    else if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Memory"))
                    {
                        ramUsagePercent = sensor.Value ?? 0;
                    }
                }

                // Chỉ lấy sensor đầu tiên hoặc có tên phù hợp nhất
                if (memoryUsedSensors.Count > 0)
                {
                    // Ưu tiên sensor có tên "Memory Used" chứa "Physical" hoặc không chứa "Virtual"
                    var preferredSensor = memoryUsedSensors.FirstOrDefault(s =>
                        s.Name.Contains("Physical") || !s.Name.Contains("Virtual")) ?? memoryUsedSensors[0];
                    usedMemory = preferredSensor.Value ?? 0;
                    Debug.WriteLine($"Selected Used Memory sensor: {preferredSensor.Name} = {usedMemory:F2} GB");
                }

                if (memoryAvailableSensors.Count > 0)
                {
                    var preferredSensor = memoryAvailableSensors.FirstOrDefault(s =>
                        s.Name.Contains("Physical") || !s.Name.Contains("Virtual")) ?? memoryAvailableSensors[0];
                    availableMemory = preferredSensor.Value ?? 0;
                    Debug.WriteLine($"Selected Available Memory sensor: {preferredSensor.Name} = {availableMemory:F2} GB");
                }

                totalMemory = usedMemory + availableMemory;

                // Kiểm tra nếu tổng > 20GB thì có vấn đề, chia đôi (có thể sensor báo cáo nhầm đơn vị)
                if (totalMemory > 20)
                {
                    Debug.WriteLine($"Total memory seems too high ({totalMemory:F2}GB), attempting to correct...");
                    // Có thể sensor báo cáo bằng MB thay vì GB
                    usedMemory = usedMemory / 1024f;
                    availableMemory = availableMemory / 1024f;
                    totalMemory = usedMemory + availableMemory;
                    Debug.WriteLine($"Corrected values - Total: {totalMemory:F2}GB, Used: {usedMemory:F2}GB, Available: {availableMemory:F2}GB");
                }

                if (totalMemory > 0)
                {
                    ramUsagePercent = (usedMemory / totalMemory) * 100;
                }
            }

            // Đảm bảo các giá trị hợp lý
            if (usedMemory < 0) usedMemory = 0;
            if (availableMemory < 0) availableMemory = 0;
            if (ramUsagePercent < 0) ramUsagePercent = 0;
            if (ramUsagePercent > 100) ramUsagePercent = 100;

            systemInfo.RamUsed = usedMemory;
            systemInfo.RamAvailable = availableMemory;
            systemInfo.RamTotal = totalMemory;

            App.Current?.Dispatcher.Invoke(() =>
            {
                if (RamUsage != null) RamUsage.Text = $"RAM Usage: {ramUsagePercent:F1}%";
                if (RamTotal != null) RamTotal.Text = $"Total RAM: {totalMemory:F2} GB";
                if (RamUsed != null) RamUsed.Text = $"Used RAM: {usedMemory:F2} GB";
                if (RamAvailable != null) RamAvailable.Text = $"Available RAM: {availableMemory:F2} GB";
            });

            Debug.WriteLine($"Final result - Total: {totalMemory:F2}GB, Used: {usedMemory:F2}GB, Available: {availableMemory:F2}GB, Usage: {ramUsagePercent:F1}%");

            return ramUsagePercent;
        }

        private float UpdateGpuInfo(IHardware hardware, SystemInfoEventArgs systemInfo)
        {
            systemInfo.GpuName = hardware.Name;

            float temperature = 0;
            float usage = 0;
            float memoryUsed = 0;
            float memoryTotal = 0;
            bool foundTemperature = false;

            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Temperature)
                {
                    if (!foundTemperature && sensor.Name.Contains("GPU"))
                    {
                        temperature = sensor.Value ?? 0;
                        foundTemperature = true;
                    }
                }
                else if (sensor.SensorType == SensorType.Load)
                {
                    if (sensor.Name.Contains("GPU Core") || sensor.Name.Contains("GPU"))
                    {
                        usage = sensor.Value ?? 0;
                    }
                }
                else if (sensor.SensorType == SensorType.SmallData)
                {
                    if (sensor.Name.Contains("GPU Memory Used"))
                    {
                        memoryUsed = sensor.Value ?? 0;
                    }
                    else if (sensor.Name.Contains("GPU Memory Total"))
                    {
                        memoryTotal = sensor.Value ?? 0;
                    }
                }
            }

            systemInfo.GpuTemperature = temperature;
            systemInfo.GpuMemoryUsed = memoryUsed;
            systemInfo.GpuMemoryTotal = memoryTotal;

            App.Current?.Dispatcher.Invoke(() =>
            {
                if (GpuName != null)
                {
                    // Hiển thị loại GPU để người dùng biết đang theo dõi GPU nào
                    string gpuType = IsDiscreteGpu(hardware) ? " (GPU rời)" : " (GPU tích hợp)";
                    GpuName.Text = $"GPU Name: {hardware.Name}{gpuType}";
                }
                if (GpuTemp != null) GpuTemp.Text = $"GPU Temperature: {temperature:F1}°C";
                if (GpuLoad != null) GpuLoad.Text = $"GPU Usage: {usage:F1}%";

                if (GpuMemory != null)
                {
                    if (memoryTotal > 0)
                    {
                        GpuMemory.Text = $"GPU Memory: {memoryUsed:F0}/{memoryTotal:F0} MB ({memoryUsed / memoryTotal * 100:F1}%)";
                    }
                    else
                    {
                        GpuMemory.Text = "GPU Memory: N/A";
                    }
                }
            });

            return usage;
        }

        private float UpdateDiskInfo(IHardware hardware, SystemInfoEventArgs systemInfo)
        {
            systemInfo.DiskName = hardware.Name;

            float temperature = 0;
            float activity = 0;
            float used = 0;
            float total = 0;
            bool foundTemperature = false;
            bool foundDiskSpace = false;

            //In ra tất cả sensors để kiểm tra
            Debug.WriteLine($"=== Disk Hardware: {hardware.Name} ===");
            foreach (var sensor in hardware.Sensors)
            {
                Debug.WriteLine($"Sensor: {sensor.Name}, Type: {sensor.SensorType}, Value: {sensor.Value}");

                if (sensor.SensorType == SensorType.Temperature)
                {
                    if (!foundTemperature)
                    {
                        temperature = sensor.Value ?? 0;
                        foundTemperature = true;
                    }
                }
                else if (sensor.SensorType == SensorType.Load)
                {
                    if (sensor.Name.Contains("Total Activity") || sensor.Name.Contains("Activity"))
                    {
                        activity = sensor.Value ?? 0;
                    }
                }
                else if (sensor.SensorType == SensorType.Data)
                {
                    // Mở rộng tìm kiếm tên sensor
                    if (sensor.Name.Contains("Used") && (sensor.Name.Contains("Space") || sensor.Name.Contains("Storage")))
                    {
                        used = sensor.Value ?? 0;
                        foundDiskSpace = true;
                        Debug.WriteLine($"Found Used Space: {used} GB");
                    }
                    else if (sensor.Name.Contains("Total") && (sensor.Name.Contains("Space") || sensor.Name.Contains("Storage") || sensor.Name.Contains("Size")))
                    {
                        total = sensor.Value ?? 0;
                        foundDiskSpace = true;
                        Debug.WriteLine($"Found Total Space: {total} GB");
                    }
                }
            }

            // Nếu không tìm thấy thông tin disk space từ LibreHardwareMonitor, sử dụng DriveInfo
            if (!foundDiskSpace || total == 0)
            {
                try
                {
                    Debug.WriteLine("Trying to get disk space from DriveInfo...");
                    var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);

                    foreach (var drive in drives)
                    {
                        // Lấy thông tin của ổ đĩa chính (thường là C:)
                        if (drive.Name.StartsWith("C:") || drives.Count() == 1)
                        {
                            total = drive.TotalSize / (1024f * 1024f * 1024f); // Convert to GB
                            used = (drive.TotalSize - drive.AvailableFreeSpace) / (1024f * 1024f * 1024f); // Convert to GB

                            Debug.WriteLine($"DriveInfo - Drive: {drive.Name}, Total: {total:F1} GB, Used: {used:F1} GB");
                            foundDiskSpace = true;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting disk info from DriveInfo: {ex.Message}");
                }
            }

            // Nếu vẫn không có, thử sử dụng WMI
            if (!foundDiskSpace || total == 0)
            {
                try
                {
                    Debug.WriteLine("Trying to get disk space from WMI...");
                    using (var searcher = new ManagementObjectSearcher("SELECT Size,FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            var size = Convert.ToDouble(obj["Size"]);
                            var freeSpace = Convert.ToDouble(obj["FreeSpace"]);

                            total = (float)(size / (1024.0 * 1024.0 * 1024.0)); // Convert to GB
                            used = (float)((size - freeSpace) / (1024.0 * 1024.0 * 1024.0)); // Convert to GB

                            Debug.WriteLine($"WMI - Total: {total:F1} GB, Used: {used:F1} GB");
                            foundDiskSpace = true;
                            break; // Chỉ lấy ổ đĩa đầu tiên
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting disk info from WMI: {ex.Message}");
                }
            }

            systemInfo.DiskTemperature = temperature;
            systemInfo.DiskUsed = used;
            systemInfo.DiskTotal = total;

            // Cập nhật UI
            App.Current?.Dispatcher.Invoke(() =>
            {
                if (DiskName != null) DiskName.Text = $"Disk Name: {hardware.Name}";
                if (DiskTemp != null) DiskTemp.Text = $"Disk Temperature: {temperature:F1}°C";
                if (DiskActivity != null) DiskActivity.Text = $"Disk Activity: {activity:F1}%";

                if (DiskSpace != null)
                {
                    if (total > 0 && foundDiskSpace)
                    {
                        float usedPercent = (used / total) * 100;
                        DiskSpace.Text = $"Disk Space: {used:F1}/{total:F1} GB ({usedPercent:F1}%)";
                    }
                    else
                    {
                        DiskSpace.Text = "Disk Space: N/A";
                    }
                }
            });

            Debug.WriteLine($"Final Disk Info - Used: {used:F1} GB, Total: {total:F1} GB, Found: {foundDiskSpace}");
            return activity;
        }

        // Phương thức public để thay đổi GPU ưu tiên thủ công (tùy chọn)
        public void SetPriorityGpu(string gpuName)
        {
            if (computer == null) return;

            var gpu = computer.Hardware.FirstOrDefault(h =>
                (h.HardwareType == HardwareType.GpuNvidia ||
                 h.HardwareType == HardwareType.GpuAmd ||
                 h.HardwareType == HardwareType.GpuIntel) &&
                h.Name.Contains(gpuName));

            if (gpu != null)
            {
                priorityGpu = gpu;
                Debug.WriteLine($"Đã thay đổi GPU ưu tiên thành: {gpu.Name}");
            }
        }

        // Phương thức để lấy danh sách tất cả GPU có sẵn
        public string[] GetAvailableGpus()
        {
            if (computer == null) return new string[0];

            return computer.Hardware
                .Where(h => h.HardwareType == HardwareType.GpuNvidia ||
                           h.HardwareType == HardwareType.GpuAmd ||
                           h.HardwareType == HardwareType.GpuIntel)
                .Select(h => h.Name)
                .ToArray();
        }
    }
}
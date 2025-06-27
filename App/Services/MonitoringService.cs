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
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing diskUsageCounter: {ex.Message}");
            }

            // Khởi tạo GPU ưu tiên
            InitializePriorityGpu();

            // Tìm PerformanceCounter cho GPU
            try
            {
                var categories = PerformanceCounterCategory.GetCategories()
                    .Where(c => c.CategoryName.Contains("GPU") ||
                                c.CategoryName.Contains("NVIDIA") ||
                                c.CategoryName.Contains("AMD") ||
                                c.CategoryName.Contains("3D"));

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
                                if (counter.CounterName.Contains("Utilization") ||
                                    counter.CounterName.Contains("Usage") ||
                                    counter.CounterName.Contains("3D"))
                                {
                                    gpuUsageCounter = new PerformanceCounter(category.CategoryName, counter.CounterName, instances[0]);
                                    Debug.WriteLine($"Found GPU PerformanceCounter: {category.CategoryName}, Counter: {counter.CounterName}, Instance: {instances[0]}");
                                    break;
                                }
                            }
                            if (gpuUsageCounter != null) break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error accessing PerformanceCounter category {category.CategoryName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing GPU PerformanceCounter: {ex.Message}");
            }
        }

        private void InitializePriorityGpu()
        {
            if (computer == null) return;

            var gpuPriorityOrder = new[]
            {
                HardwareType.GpuNvidia,
                HardwareType.GpuAmd,
                HardwareType.GpuIntel
            };

            foreach (var gpuType in gpuPriorityOrder)
            {
                var gpu = computer.Hardware.FirstOrDefault(h => h.HardwareType == gpuType);
                if (gpu != null)
                {
                    gpu.Update();
                    if (IsDiscreteGpu(gpu))
                    {
                        priorityGpu = gpu;
                        Debug.WriteLine($"Selected priority GPU (discrete): {gpu.Name}");
                        return;
                    }
                    else if (priorityGpu == null)
                    {
                        priorityGpu = gpu;
                        Debug.WriteLine($"Selected fallback GPU: {gpu.Name}");
                    }
                }
            }

            if (priorityGpu != null)
            {
                Debug.WriteLine($"Final selected GPU: {priorityGpu.Name}");
            }
            else
            {
                Debug.WriteLine("No GPU found for monitoring.");
            }
        }

        private bool IsDiscreteGpu(IHardware gpu)
        {
            string name = gpu.Name.ToLower();
            string[] discreteKeywords = {
                "geforce", "rtx", "gtx", "radeon", "rx ", "r9", "r7", "r5",
                "titan", "quadro", "firepro", "vega", "fury"
            };
            string[] integratedKeywords = {
                "intel", "uhd", "hd graphics", "iris", "integrated",
                "apu", "ryzen", "vega 3", "vega 5", "vega 6", "vega 7", "vega 8"
            };

            if (integratedKeywords.Any(keyword => name.Contains(keyword)))
            {
                return false;
            }

            if (discreteKeywords.Any(keyword => name.Contains(keyword)))
            {
                return true;
            }

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
                Debug.WriteLine("Monitoring stopped due to cancellation.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Monitoring error: {ex.Message}");
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

            // Cập nhật thông tin GPU
            systemInfo.GpuUsage = UpdateGpuInfo(systemInfo);

            // Fallback cho GPU usage nếu không lấy được từ LibreHardwareMonitor
            if (systemInfo.GpuUsage == 0 && gpuUsageCounter != null)
            {
                try
                {
                    systemInfo.GpuUsage = gpuUsageCounter.NextValue();
                    Debug.WriteLine($"GPU Usage from PerformanceCounter: {systemInfo.GpuUsage:F1}%");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting GPU Usage from PerformanceCounter: {ex.Message}");
                }
            }

            // Fallback cho Disk usage
            if (systemInfo.DiskUsage == 0 && diskUsageCounter != null)
            {
                try
                {
                    systemInfo.DiskUsage = diskUsageCounter.NextValue();
                    if (systemInfo.DiskUsage > 100) systemInfo.DiskUsage = 100;
                    Debug.WriteLine($"Disk Usage from PerformanceCounter: {systemInfo.DiskUsage:F1}%");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting Disk Usage from PerformanceCounter: {ex.Message}");
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

        private float UpdateCpuInfo(IHardware hardware, SystemInfoEventArgs systemInfo)
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
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting CPU temperature from WMI: {ex.Message}");
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

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        totalMemory = Convert.ToSingle(obj["TotalPhysicalMemory"]) / (1024f * 1024f * 1024f);
                        Debug.WriteLine($"WMI Total Physical Memory: {totalMemory:F2} GB");
                        break;
                    }
                }

                if (availableMemoryCounter != null)
                {
                    availableMemory = availableMemoryCounter.NextValue() / 1024.0f;
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

                if (memoryUsedSensors.Count > 0)
                {
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

                if (totalMemory > 20)
                {
                    Debug.WriteLine($"Total memory seems too high ({totalMemory:F2}GB), attempting to correct...");
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

        private float UpdateGpuInfo(SystemInfoEventArgs systemInfo)
        {
            float maxUsage = 0;
            IHardware? selectedGpu = null;

            var gpus = computer.Hardware.Where(h =>
                h.HardwareType == HardwareType.GpuNvidia ||
                h.HardwareType == HardwareType.GpuAmd ||
                h.HardwareType == HardwareType.GpuIntel).ToList();

            if (!gpus.Any())
            {
                Debug.WriteLine("No GPUs found for monitoring.");
                systemInfo.GpuName = "N/A";
                systemInfo.GpuTemperature = 0;
                systemInfo.GpuMemoryUsed = 0;
                systemInfo.GpuMemoryTotal = 0;
                return 0;
            }

            foreach (var gpu in gpus)
            {
                gpu.Update();
                float temperature = 0;
                float usage = 0;
                float memoryUsed = 0;
                float memoryTotal = 0;
                bool foundTemperature = false;
                bool foundCoreUsage = false;

                foreach (var sensor in gpu.Sensors)
                {
                    Debug.WriteLine($"GPU: {gpu.Name}, Sensor: {sensor.Name}, Type: {sensor.SensorType}, Value: {sensor.Value}");

                    if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("GPU"))
                    {
                        if (!foundTemperature)
                        {
                            temperature = sensor.Value ?? 0;
                            foundTemperature = true;
                        }
                    }
                    else if (sensor.SensorType == SensorType.Load)
                    {
                        // Ưu tiên cảm biến GPU Core
                        if (sensor.Name.Contains("GPU Core"))
                        {
                            usage = sensor.Value ?? 0;
                            foundCoreUsage = true;
                            Debug.WriteLine($"Selected GPU Core Usage: {usage:F1}%");
                        }
                        // Chỉ xem xét D3D hoặc Video Engine nếu chưa tìm thấy GPU Core
                        else if (!foundCoreUsage && (sensor.Name.Contains("D3D") || sensor.Name.Contains("Video Engine")))
                        {
                            usage = sensor.Value ?? 0;
                            Debug.WriteLine($"Selected Fallback Usage (D3D/Video Engine): {usage:F1}%");
                        }
                        // Bỏ qua cảm biến GPU Memory
                        else if (sensor.Name.Contains("GPU Memory"))
                        {
                            Debug.WriteLine($"Ignoring GPU Memory Load sensor: {sensor.Name}, Value: {sensor.Value}");
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

                // Chỉ cập nhật maxUsage nếu tìm thấy cảm biến GPU Core hoặc D3D/Video Engine
                if (foundCoreUsage || usage > 0)
                {
                    if (usage > maxUsage)
                    {
                        maxUsage = usage;
                        selectedGpu = gpu;
                        systemInfo.GpuName = gpu.Name;
                        systemInfo.GpuTemperature = temperature;
                        systemInfo.GpuMemoryUsed = memoryUsed;
                        systemInfo.GpuMemoryTotal = memoryTotal;
                    }
                }

                Debug.WriteLine($"GPU: {gpu.Name}, Usage: {usage:F1}%, VRAM: {(memoryTotal > 0 ? (memoryUsed / memoryTotal * 100) : 0):F1}%");
            }

            // Kiểm tra priorityGpu nếu không tìm thấy usage
            if (maxUsage == 0 && priorityGpu != null)
            {
                priorityGpu.Update();
                float temperature = 0;
                float usage = 0;
                float memoryUsed = 0;
                float memoryTotal = 0;
                bool foundTemperature = false;
                bool foundCoreUsage = false;

                foreach (var sensor in priorityGpu.Sensors)
                {
                    Debug.WriteLine($"Priority GPU: {priorityGpu.Name}, Sensor: {sensor.Name}, Type: {sensor.SensorType}, Value: {sensor.Value}");

                    if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("GPU"))
                    {
                        if (!foundTemperature)
                        {
                            temperature = sensor.Value ?? 0;
                            foundTemperature = true;
                        }
                    }
                    else if (sensor.SensorType == SensorType.Load)
                    {
                        if (sensor.Name.Contains("GPU Core"))
                        {
                            usage = sensor.Value ?? 0;
                            foundCoreUsage = true;
                            Debug.WriteLine($"Selected Priority GPU Core Usage: {usage:F1}%");
                        }
                        else if (!foundCoreUsage && (sensor.Name.Contains("D3D") || sensor.Name.Contains("Video Engine")))
                        {
                            usage = sensor.Value ?? 0;
                            Debug.WriteLine($"Selected Priority Fallback Usage (D3D/Video Engine): {usage:F1}%");
                        }
                        else if (sensor.Name.Contains("GPU Memory"))
                        {
                            Debug.WriteLine($"Ignoring GPU Memory Load sensor: {sensor.Name}, Value: {sensor.Value}");
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

                if (foundCoreUsage || usage > 0)
                {
                    maxUsage = usage;
                    selectedGpu = priorityGpu;
                    systemInfo.GpuName = priorityGpu.Name;
                    systemInfo.GpuTemperature = temperature;
                    systemInfo.GpuMemoryUsed = memoryUsed;
                    systemInfo.GpuMemoryTotal = memoryTotal;
                }
            }

            // Cập nhật UI
            App.Current?.Dispatcher.Invoke(() =>
            {
                if (GpuName != null)
                {
                    string gpuType = selectedGpu != null && IsDiscreteGpu(selectedGpu) ? " (GPU rời)" : " (GPU tích hợp)";
                    GpuName.Text = $"GPU Name: {(selectedGpu != null ? selectedGpu.Name : "N/A")}{gpuType}";
                }
                if (GpuTemp != null) GpuTemp.Text = $"GPU Temperature: {systemInfo.GpuTemperature:F1}°C";
                if (GpuLoad != null) GpuLoad.Text = $"GPU Usage: {maxUsage:F1}%";

                if (GpuMemory != null)
                {
                    if (systemInfo.GpuMemoryTotal > 0)
                    {
                        float vramUsagePercent = (systemInfo.GpuMemoryUsed / systemInfo.GpuMemoryTotal) * 100;
                        GpuMemory.Text = $"GPU Memory: {systemInfo.GpuMemoryUsed:F0}/{systemInfo.GpuMemoryTotal:F0} MB ({vramUsagePercent:F1}%)";
                    }
                    else
                    {
                        GpuMemory.Text = "GPU Memory: N/A";
                    }
                }
            });

            Debug.WriteLine($"Final GPU Usage: {maxUsage:F1}%, VRAM Usage: {(systemInfo.GpuMemoryTotal > 0 ? (systemInfo.GpuMemoryUsed / systemInfo.GpuMemoryTotal * 100) : 0):F1}%");
            return maxUsage;
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

            if (!foundDiskSpace || total == 0)
            {
                try
                {
                    Debug.WriteLine("Trying to get disk space from DriveInfo...");
                    var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);

                    foreach (var drive in drives)
                    {
                        if (drive.Name.StartsWith("C:") || drives.Count() == 1)
                        {
                            total = drive.TotalSize / (1024f * 1024f * 1024f);
                            used = (drive.TotalSize - drive.AvailableFreeSpace) / (1024f * 1024f * 1024f);
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

                            total = (float)(size / (1024.0 * 1024.0 * 1024.0));
                            used = (float)((size - freeSpace) / (1024.0 * 1024.0 * 1024.0));
                            Debug.WriteLine($"WMI - Total: {total:F1} GB, Used: {used:F1} GB");
                            foundDiskSpace = true;
                            break;
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
                Debug.WriteLine($"Changed priority GPU to: {gpu.Name}");
            }
        }

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
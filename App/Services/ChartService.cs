using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using SystemMonitor.Models;
using SystemMonitor.Services;
using System.Linq;

namespace SystemMonitor.Services
{
    public class ChartService
    {
        private PowerManagementService _powerManagementService;

        private ObservableCollection<DataPoint> _cpuDataPoints;
        private ObservableCollection<DataPoint> _ramDataPoints;
        private ObservableCollection<DataPoint> _gpuDataPoints;
        private ObservableCollection<DataPoint> _diskDataPoints;
        private int _maxDataPoints = 60;

        private readonly CartesianChart CpuChart;
        private readonly CartesianChart RamChart;
        private readonly CartesianChart GpuChart;
        private readonly CartesianChart DiskChart;

        public TextBlock CpuLoad { get; set; }
        public TextBlock RamUsage { get; set; }
        public TextBlock GpuLoad { get; set; }
        public TextBlock DiskActivity { get; set; }

        public TextBlock CurrentMinCpuFreq { get; set; }
        public TextBlock CurrentMaxCpuFreq { get; set; }

        public ChartService(CartesianChart cpuChart, CartesianChart ramChart,
                            CartesianChart gpuChart, CartesianChart diskChart)
        {
            CpuChart = cpuChart;
            RamChart = ramChart;
            GpuChart = gpuChart;
            DiskChart = diskChart;

            InitializeCharts();
        }

        public void SetUIControls(TextBlock cpuLoad, TextBlock ramUsage,
                                 TextBlock gpuLoad, TextBlock diskActivity,
                                 TextBlock currentMinCpuFreq, TextBlock currentMaxCpuFreq)
        {
            CpuLoad = cpuLoad;
            RamUsage = ramUsage;
            GpuLoad = gpuLoad;
            DiskActivity = diskActivity;
            CurrentMinCpuFreq = currentMinCpuFreq;
            CurrentMaxCpuFreq = currentMaxCpuFreq;
        }

        public void SetPowerManagementService(PowerManagementService powerManagementService)
        {
            _powerManagementService = powerManagementService;
        }

        private void InitializeCharts()
        {
            _cpuDataPoints = new ObservableCollection<DataPoint>();
            _ramDataPoints = new ObservableCollection<DataPoint>();
            _gpuDataPoints = new ObservableCollection<DataPoint>();
            _diskDataPoints = new ObservableCollection<DataPoint>();

            var mapper = Mappers.Xy<DataPoint>()
                .X(point => point.DateTime.Ticks)
                .Y(point => point.Value);

            Charting.For<DataPoint>(mapper);

            CpuChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "CPU Usage",
                    Values = new ChartValues<DataPoint>(),
                    PointGeometry = null,
                    LineSmoothness = 0.3,
                    Stroke = Brushes.DodgerBlue,
                    Fill = new SolidColorBrush(Color.FromArgb(64, 30, 144, 255))
                }
            };

            RamChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "RAM Usage",
                    Values = new ChartValues<DataPoint>(),
                    PointGeometry = null,
                    LineSmoothness = 0.3,
                    Stroke = Brushes.ForestGreen,
                    Fill = new SolidColorBrush(Color.FromArgb(64, 34, 139, 34))
                }
            };

            // CPU Chart Axes - Chỉ cấu hình trục X, để XAML xử lý trục Y
            CpuChart.AxisX.Add(new Axis
            {
                ShowLabels = false,
            });

            // Không thêm AxisY ở đây - để XAML xử lý

            // RAM Chart Axes - Chỉ cấu hình trục X, để XAML xử lý trục Y
            RamChart.AxisX.Add(new Axis
            {
                ShowLabels = false,
            });

            // Không thêm AxisY ở đây - để XAML xử lý

            GpuChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "GPU Usage",
                    Values = new ChartValues<DataPoint>(),
                    PointGeometry = null,
                    LineSmoothness = 0.3,
                    Stroke = Brushes.DarkOrange,
                    Fill = new SolidColorBrush(Color.FromArgb(64, 255, 140, 0))
                }
            };

            DiskChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Disk Activity",
                    Values = new ChartValues<DataPoint>(),
                    PointGeometry = null,
                    LineSmoothness = 0.3,
                    Stroke = Brushes.Purple,
                    Fill = new SolidColorBrush(Color.FromArgb(64, 128, 0, 128))
                }
            };

            // GPU Chart Axes - Chỉ cấu hình trục X, để XAML xử lý trục Y
            GpuChart.AxisX.Add(new Axis
            {
                ShowLabels = false,
            });

            // Không thêm AxisY ở đây - để XAML xử lý

            // Disk Chart Axes - Chỉ cấu hình trục X, để XAML xử lý trục Y
            DiskChart.AxisX.Add(new Axis
            {
                ShowLabels = false,
            });

            // Không thêm AxisY ở đây - để XAML xử lý
        }

        public void UpdateCharts(float cpuUsage, float ramUsage, float gpuUsage, float diskUsage)
        {
            // Use the Dispatcher from any of the charts (they are UI elements)
            var dispatcher = CpuChart.Dispatcher;
            if (!dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => UpdateCharts(cpuUsage, ramUsage, gpuUsage, diskUsage));
                return;
            }

            var now = DateTime.Now;

            _cpuDataPoints.Add(new DataPoint(now, cpuUsage));
            _ramDataPoints.Add(new DataPoint(now, ramUsage));
            _gpuDataPoints.Add(new DataPoint(now, gpuUsage));
            _diskDataPoints.Add(new DataPoint(now, diskUsage));

            while (_cpuDataPoints.Count > _maxDataPoints)
                _cpuDataPoints.RemoveAt(0);
            while (_ramDataPoints.Count > _maxDataPoints)
                _ramDataPoints.RemoveAt(0);
            while (_gpuDataPoints.Count > _maxDataPoints)
                _gpuDataPoints.RemoveAt(0);
            while (_diskDataPoints.Count > _maxDataPoints)
                _diskDataPoints.RemoveAt(0);

            if (CpuLoad != null)
                CpuLoad.Text = $"CPU Usage: {cpuUsage:F1}%";
            if (RamUsage != null)
                RamUsage.Text = $"RAM Usage: {ramUsage:F1}%";
            if (GpuLoad != null)
                GpuLoad.Text = $"GPU Usage: {gpuUsage:F1}%";
            if (DiskActivity != null)
                DiskActivity.Text = $"Disk Activity: {diskUsage:F1}%";

            var cpuValues = (ChartValues<DataPoint>)CpuChart.Series[0].Values;
            var ramValues = (ChartValues<DataPoint>)RamChart.Series[0].Values;
            var gpuValues = (ChartValues<DataPoint>)GpuChart.Series[0].Values;
            var diskValues = (ChartValues<DataPoint>)DiskChart.Series[0].Values;

            cpuValues.Clear();
            ramValues.Clear();
            gpuValues.Clear();
            diskValues.Clear();

            foreach (var point in _cpuDataPoints)
                cpuValues.Add(point);
            foreach (var point in _ramDataPoints)
                ramValues.Add(point);
            foreach (var point in _gpuDataPoints)
                gpuValues.Add(point);
            foreach (var point in _diskDataPoints)
                diskValues.Add(point);

            if (_cpuDataPoints.Any())
            {
                DateTime oldestPoint = _cpuDataPoints.First().DateTime;
                DateTime newestPoint = _cpuDataPoints.Last().DateTime.AddSeconds(2);

                CpuChart.AxisX[0].MinValue = oldestPoint.Ticks;
                CpuChart.AxisX[0].MaxValue = newestPoint.Ticks;
                RamChart.AxisX[0].MinValue = oldestPoint.Ticks;
                RamChart.AxisX[0].MaxValue = newestPoint.Ticks;
                GpuChart.AxisX[0].MinValue = oldestPoint.Ticks;
                GpuChart.AxisX[0].MaxValue = newestPoint.Ticks;
                DiskChart.AxisX[0].MinValue = oldestPoint.Ticks;
                DiskChart.AxisX[0].MaxValue = newestPoint.Ticks;
            }
        }

        public void UpdateDvfsInfo()
        {
            var dispatcher = CurrentMinCpuFreq?.Dispatcher ?? CurrentMaxCpuFreq?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(UpdateDvfsInfo);
                return;
            }

            try
            {
                if (_powerManagementService != null)
                {
                    if (CurrentMinCpuFreq != null)
                        CurrentMinCpuFreq.Text = $"Current minimum frequency: {_powerManagementService.savedMinFrequency}%";
                    if (CurrentMaxCpuFreq != null)
                        CurrentMaxCpuFreq.Text = $"Current maximum frequency: {_powerManagementService.savedMaxFrequency}%";
                }
            }
            catch
            {
                if (CurrentMinCpuFreq != null)
                    CurrentMinCpuFreq.Text = "Current minimum frequency: Display error";
                if (CurrentMaxCpuFreq != null)
                    CurrentMaxCpuFreq.Text = "Current maximum frequency: Display error";
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SystemMonitor.Services;

namespace SystemMonitor
{
    /// <summary>
    /// Interaction logic for SolutionGuideWindow.xaml
    /// </summary>
    public partial class SolutionGuideWindow : Window
    {
        private readonly WarningService.SolutionRecommendation _solution;

        public SolutionGuideWindow(WarningService.SolutionRecommendation solution)
        {
            InitializeComponent();
            _solution = solution;
            SetupGuideContent(solution);
        }

        private void SetupGuideContent(WarningService.SolutionRecommendation solution)
        {
            // Cập nhật tiêu đề và loại giải pháp
            if (FindName("SolutionTitle") is TextBlock titleBlock)
            {
                titleBlock.Text = solution.Title;
            }
            if (FindName("SolutionType") is TextBlock typeBlock)
            {
                typeBlock.Text = GetSolutionType(solution.Title);
            }

            // Lấy nội dung chi tiết từ GetDetailedGuide
            var (description, steps, warnings, tips) = ParseDetailedGuide(GetDetailedGuide(solution));

            // Cập nhật mô tả giải pháp
            if (FindName("SolutionDescription") is TextBlock descBlock)
            {
                descBlock.Text = description;
            }

            // Cập nhật các bước thực hiện
            if (FindName("GuideContent") is TextBlock guideBlock)
            {
                guideBlock.Text = steps;
            }

            // Cập nhật lưu ý
            if (FindName("WarningContent") is TextBlock warningBlock)
            {
                warningBlock.Text = warnings;
            }

            // Cập nhật tài nguyên bổ sung (mẹo)
            if (FindName("ResourcesContent") is TextBlock resourcesBlock)
            {
                resourcesBlock.Text = tips;
            }

            // Thêm nút hành động nếu có ActionType và ActionData
            if (!string.IsNullOrEmpty(solution.ActionType) && !string.IsNullOrEmpty(solution.ActionData))
            {
                AddActionButton(solution);
            }
        }

        private string GetSolutionType(string title)
        {
            // Xác định loại giải pháp dựa trên tiêu đề
            if (title.Contains("CPU") || title.Contains("Smart Mode") || title.Contains("Power Plan") || title.Contains("Frequency") || title.Contains("Process"))
                return "Khắc phục sự cố CPU";
            if (title.Contains("Temp"))
                return "Khắc phục sự cố nhiệt độ";
            if (title.Contains("RAM"))
                return "Khắc phục sự cố RAM";
            if (title.Contains("GPU"))
                return "Khắc phục sự cố GPU";
            if (title.Contains("Disk"))
                return "Khắc phục sự cố ổ đĩa";
            return "Khắc phục sự cố hệ thống";
        }

        private (string Description, string Steps, string Warnings, string Tips) ParseDetailedGuide(string guide)
        {
            // Phân tách nội dung từ GetDetailedGuide thành các phần
            var lines = guide.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var description = new StringBuilder();
            var steps = new StringBuilder();
            var warnings = new StringBuilder();
            var tips = new StringBuilder();
            string currentSection = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("📋"))
                    currentSection = "Description";
                else if (line.StartsWith("📝"))
                    currentSection = "Steps";
                else if (line.StartsWith("⚠️"))
                    currentSection = "Warnings";
                else if (line.StartsWith("💡"))
                    currentSection = "Tips";
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    switch (currentSection)
                    {
                        case "Description":
                            description.AppendLine(line);
                            break;
                        case "Steps":
                            steps.AppendLine(line);
                            break;
                        case "Warnings":
                            warnings.AppendLine(line);
                            break;
                        case "Tips":
                            tips.AppendLine(line);
                            break;
                    }
                }
            }

            return (description.ToString().Trim(), steps.ToString().Trim(), warnings.ToString().Trim(), tips.ToString().Trim());
        }

        private void AddActionButton(WarningService.SolutionRecommendation solution)
        {
            if (FindName("ActionButtonsPanel") is StackPanel actionPanel)
            {
                var button = new Button
                {
                    Content = $"Thực hiện: {solution.Action}",
                    Style = FindResource("PrimaryButtonStyle") as Style,
                    Margin = new Thickness(0, 0, 10, 0),
                    MinWidth = 150
                };
                button.Click += (s, e) => ExecuteAction(solution.ActionType, solution.ActionData);
                actionPanel.Children.Insert(0, button); // Thêm nút trước nút "Đóng"
            }
        }

        private void ExecuteAction(string actionType, string actionData)
        {
            try
            {
                switch (actionType)
                {
                    case "TAB_SWITCH":
                        // Giả sử có một cơ chế để chuyển tab trong ứng dụng chính
                        MessageBox.Show($"Chuyển sang tab: {actionData}", "Hành động", MessageBoxButton.OK, MessageBoxImage.Information);
                        // TODO: Gọi phương thức trong MainWindow để chuyển tab
                        break;
                    case "OPEN_TASK_MANAGER":
                        Process.Start("taskmgr.exe");
                        break;
                    case "RUN_DEFENDER":
                        Process.Start("windowsdefender://");
                        break;
                    default:
                        MessageBox.Show("Hành động này chưa được hỗ trợ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thực hiện hành động: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private string GetDetailedGuide(WarningService.SolutionRecommendation solution)
        {
            switch (solution.Title)
            {
                // CPU Solutions
                case "Chuyển Power Plan":
                    return @"📋 Mục đích:
Chuyển sang chế độ Power Plan phù hợp giúp giảm tải CPU và tiết kiệm năng lượng, đặc biệt khi CPU usage cao hoặc máy quá nóng.

📝 Các bước thực hiện:
1. Mở ứng dụng System Monitor.
2. Chuyển sang tab 'Power Settings'.
3. Tìm dropdown menu 'Power Plan'.
4. Chọn 'Power Saver' để giảm tối đa CPU load hoặc 'Balanced' để cân bằng giữa hiệu năng và tiết kiệm.
5. Nhấn 'Apply' để lưu thay đổi.

⚙️ Các loại Power Plan:
• High Performance: Tối ưu hiệu năng, tiêu thụ nhiều năng lượng.
• Balanced: Cân bằng giữa hiệu năng và tiết kiệm năng lượng.
• Power Saver: Giảm CPU usage và nhiệt độ, phù hợp khi làm việc nhẹ.

⚠️ Lưu ý:
• Chọn 'Power Saver' khi CPU usage > 80% hoặc nhiệt độ > 80°C.
• Chuyển sang 'High Performance' khi cần chạy ứng dụng nặng (gaming, rendering).
• Một số máy có thể yêu cầu khởi động lại để áp dụng Power Plan.

✅ Kết quả mong đợi:
• Giảm 20-30% CPU usage trong 1-2 phút.
• Nhiệt độ CPU giảm 5-10°C.
• Tăng thời lượng pin 30-50% trên laptop.

💡 Mẹo:
• Kiểm tra CPU usage trong tab 'Process Monitor' để đảm bảo hiệu quả.
• Nếu không thấy thay đổi, thử khởi động lại ứng dụng System Monitor.";

                case "Giảm CPU Frequency":
                    return @"📋 Mục đích:
Giảm tần số CPU (frequency) để hạ nhiệt độ và giảm tải CPU, đặc biệt khi CPU liên tục hoạt động ở mức cao.

📝 Các bước thực hiện:
1. Mở ứng dụng System Monitor.
2. Chuyển sang tab 'Power Settings'.
3. Tìm mục 'Max CPU Frequency'.
4. Nhập giá trị từ 60-80% (khuyến nghị 70% để cân bằng hiệu năng và nhiệt độ).
5. Nhấn 'Set' để áp dụng thay đổi.
6. Theo dõi CPU usage và nhiệt độ trong tab 'System Status'.

⚙️ Mức độ khuyến nghị:
• CPU quá nóng (>85°C): 60-65%.
• CPU hơi nóng (75-85°C): 70-75%.
• CPU bình thường (<75°C): 80-90%.

⚠️ Lưu ý:
• Giảm quá thấp (<50%) có thể làm hệ thống chậm hoặc ứng dụng bị lag.
• Thử giảm từng bước 5% để tìm mức tối ưu.
• Có thể reset về 100% nếu cần hiệu năng cao trở lại.

✅ Kết quả mong đợi:
• Nhiệt độ CPU giảm 10-20°C trong 2-3 phút.
• CPU usage giảm đáng kể khi chạy ứng dụng nhẹ.

💡 Mẹo:
• Sử dụng HWMonitor hoặc CoreTemp để kiểm tra nhiệt độ CPU trong thời gian thực.
• Nếu máy vẫn nóng, kết hợp với giải pháp 'Kiểm tra tản nhiệt'.";

                case "Bật Smart Mode":
                    return @"📋 Mục đích:
Smart Mode tự động điều chỉnh CPU frequency dựa trên tải hệ thống, giúp giảm nhiệt độ và tiết kiệm năng lượng mà không cần can thiệp thủ công.

📝 Các bước thực hiện:
1. Mở ứng dụng System Monitor.
2. Chuyển sang tab 'Power Settings'.
3. Tìm nút 'Enable Smart Mode'.
4. Nhấn nút để kích hoạt.
5. Theo dõi CPU usage và nhiệt độ trong tab 'System Status' để kiểm tra hiệu quả.

⚠️ Lưu ý:
• Smart Mode có thể làm giảm hiệu năng trong các tác vụ nặng (gaming, video rendering).
• Có thể tắt Smart Mode bằng nút 'Disable Smart Mode' khi cần hiệu năng tối đa.
• Đảm bảo ứng dụng System Monitor đang chạy để Smart Mode hoạt động.

✅ Kết quả mong đợi:
• CPU usage và nhiệt độ giảm trong 30 giây - 1 phút.
• Tiết kiệm pin 20-30% trên laptop.
• Giảm tiếng ồn quạt tản nhiệt.

💡 Mẹo:
• Kết hợp Smart Mode với Power Plan 'Balanced' để tối ưu hóa.
• Kiểm tra log trong System Monitor để xem các thay đổi CPU frequency.";

                case "Quản lý Process":
                    return @"📋 Mục đích:
Đóng các process không cần thiết để giảm CPU usage, giải phóng tài nguyên hệ thống và cải thiện hiệu năng.

📝 Các bước thực hiện:
1. Mở ứng dụng System Monitor.
2. Chuyển sang tab 'Process Monitor'.
3. Nhấn 'Refresh' để cập nhật danh sách process.
4. Sắp xếp process theo cột 'CPU%' (từ cao xuống thấp).
5. Tìm process sử dụng CPU > 20% mà không cần thiết (ví dụ: browser, game không sử dụng).
6. Right-click trên process và chọn 'End Process'.
7. Xác nhận hành động nếu được yêu cầu.

⚠️ CẢNH BÁO:
• KHÔNG đóng các process hệ thống như: System, System Idle Process, winlogon, csrss.
• Tránh đóng process của antivirus hoặc driver (thường nằm trong System32).
• Đảm bảo bạn biết process đó là gì trước khi đóng.

✅ An toàn khi đóng:
• Trình duyệt (chrome.exe, firefox.exe) khi không sử dụng.
• Ứng dụng nền (background updaters) không cần thiết.
• Game hoặc ứng dụng không đang chạy.

✅ Kết quả mong đợi:
• Giảm CPU usage 20-50% ngay lập tức.
• Hệ thống phản hồi nhanh hơn, đặc biệt khi đa nhiệm.

💡 Mẹo:
• Kiểm tra tab 'Process Monitor' định kỳ để phát hiện process bất thường.
• Nếu không chắc chắn về process, tra cứu tên process trên Google trước khi đóng.";

                case "Tắt Startup Programs":
                    return @"📋 Mục đích:
Vô hiệu hóa các chương trình khởi động cùng hệ thống để giảm CPU và RAM usage khi khởi động máy, cải thiện tốc độ khởi động.

📝 Các bước thực hiện:
1. Nhấn Ctrl + Shift + Esc để mở Task Manager.
2. Chuyển sang tab 'Startup'.
3. Xem danh sách các chương trình khởi động cùng Windows.
4. Tìm chương trình không cần thiết (ví dụ: ứng dụng chat, updater).
5. Right-click và chọn 'Disable' để ngăn chương trình khởi động tự động.
6. Khởi động lại máy để kiểm tra hiệu quả.

⚠️ Lưu ý:
• Không tắt các chương trình hệ thống hoặc driver (ví dụ: Intel, NVIDIA).
• Chỉ tắt các ứng dụng bạn nhận ra và không cần chạy khi khởi động.
• Một số ứng dụng có thể được bật lại trong cài đặt của chính nó.

✅ Kết quả mong đợi:
• Giảm thời gian khởi động máy 10-30 giây.
• Giảm CPU/RAM usage khi khởi động 10-20%.

💡 Mẹo:
• Kiểm tra tác động của chương trình trong cột 'Startup Impact' trong Task Manager.
• Nếu cần bật lại, quay lại tab 'Startup' và chọn 'Enable'.";

                case "Kiểm tra Malware":
                    return @"📋 Mục đích:
Quét và loại bỏ phần mềm độc hại (malware) có thể gây ra CPU, RAM hoặc Disk usage cao bất thường.

📝 Các bước thực hiện:
1. Mở Windows Security (nhấn Windows + S, gõ 'Windows Security').
2. Chọn 'Virus & threat protection'.
3. Nhấn 'Scan options'.
4. Chọn 'Full Scan' và nhấn 'Scan now'.
5. Chờ quá trình quét hoàn tất (có thể mất 30-60 phút).
6. Làm theo hướng dẫn để cách ly hoặc xóa các mối đe dọa nếu được phát hiện.

⚠️ Lưu ý:
• Đảm bảo Windows Defender hoặc phần mềm antivirus được cập nhật.
• Ngắt kết nối internet nếu nghi ngờ malware đang hoạt động.
• Sao lưu dữ liệu quan trọng trước khi xóa bất kỳ file nào.

✅ Kết quả mong đợi:
• Loại bỏ malware gây tiêu tốn tài nguyên.
• Giảm CPU/RAM usage nếu malware là nguyên nhân.

💡 Mẹo:
• Cài đặt thêm công cụ như Malwarebytes để quét sâu hơn.
• Tránh tải phần mềm từ nguồn không đáng tin cậy.";

                // CPU Temp Solutions
                case "Giảm CPU Performance":
                    return @"📋 Mục đích:
Hạ hiệu năng CPU để giảm nhiệt độ, đặc biệt khi CPU quá nóng (>85°C).

📝 Các bước thực hiện:
1. Mở ứng dụng System Monitor.
2. Chuyển sang tab 'Power Settings'.
3. Tìm mục 'Max CPU Frequency'.
4. Nhập giá trị 60-70% (khuyến nghị 65% để giảm nhiệt hiệu quả).
5. Nhấn 'Set' để áp dụng.
6. Theo dõi nhiệt độ CPU trong tab 'System Status'.

⚠️ Lưu ý:
• Giảm hiệu năng có thể làm chậm ứng dụng nặng.
• Không đặt dưới 50% trừ khi thực sự cần thiết.
• Kết hợp với giải pháp 'Kiểm tra tản nhiệt' nếu nhiệt độ vẫn cao.

✅ Kết quả mong đợi:
• Nhiệt độ CPU giảm 10-20°C trong 2-5 phút.
• Giảm tiếng ồn quạt tản nhiệt.

💡 Mẹo:
• Sử dụng HWMonitor hoặc CoreTemp để kiểm tra nhiệt độ chính xác.
• Nếu nhiệt độ vẫn cao, kiểm tra quạt hoặc keo tản nhiệt.";

                case "Power Saver Mode":
                    return @"📋 Mục đích:
Chuyển sang chế độ tiết kiệm năng lượng để giảm nhiệt độ CPU và tiết kiệm pin.

📝 Các bước thực hiện:
1. Mở ứng dụng System Monitor.
2. Chuyển sang tab 'Power Settings'.
3. Tìm dropdown 'Power Plan'.
4. Chọn 'Power Saver'.
5. Nhấn 'Apply' để lưu thay đổi.
6. Theo dõi nhiệt độ CPU trong tab 'System Status'.

⚠️ Lưu ý:
• Power Saver có thể làm giảm hiệu năng khi chơi game hoặc render.
• Phù hợp cho công việc nhẹ như duyệt web, soạn thảo văn bản.
• Có thể chuyển lại 'Balanced' hoặc 'High Performance' khi cần.

✅ Kết quả mong đợi:
• Nhiệt độ CPU giảm 5-15°C.
• Tiết kiệm pin 30-50% trên laptop.

💡 Mẹo:
• Kết hợp với 'Giảm CPU Frequency' để tối ưu hóa.
• Đảm bảo máy được đặt ở nơi thoáng khí.";

                case "Kiểm tra tản nhiệt":
                    return @"📋 Mục đích:
Vệ sinh và bảo trì hệ thống tản nhiệt để giảm nhiệt độ CPU, đảm bảo hiệu năng ổn định.

📝 Các bước thực hiện:
1. Tắt máy tính và rút nguồn điện.
2. Mở case máy tính (hoặc phần dưới của laptop).
3. Sử dụng bình khí nén để thổi bụi khỏi quạt CPU và tản nhiệt.
4. Kiểm tra keo tản nhiệt: nếu khô hoặc cũ, thay keo mới.
5. Lắp lại case và khởi động máy.
6. Theo dõi nhiệt độ CPU trong System Monitor.

⚠️ Lưu ý:
• Ngắt nguồn điện trước khi vệ sinh.
• Sử dụng khí nén đúng cách, tránh làm hỏng linh kiện.
• Nếu không chắc chắn, liên hệ kỹ thuật viên chuyên nghiệp.

✅ Kết quả mong đợi:
• Nhiệt độ CPU giảm 10-30°C sau khi vệ sinh.
• Quạt chạy êm hơn, hiệu năng ổn định hơn.

💡 Mẹo:
• Vệ sinh tản nhiệt 6 tháng/lần.
• Sử dụng keo tản nhiệt chất lượng cao (như Arctic MX-4).";

                case "Tăng luồng gió":
                    return @"📋 Mục đích:
Cải thiện luồng không khí trong case để giảm nhiệt độ CPU và GPU.

📝 Các bước thực hiện:
1. Kiểm tra vị trí case/laptop: đảm bảo không bị chặn bởi vật cản.
2. Đặt case ở nơi thoáng khí, cách tường ít nhất 10cm.
3. Mở System Monitor, vào tab 'System Status' để kiểm tra nhiệt độ.
4. Nếu có quạt case bổ sung, đảm bảo chúng hoạt động.
5. Sử dụng quạt tản nhiệt bên ngoài (đối với laptop) nếu cần.

⚠️ Lưu ý:
• Không đặt laptop trên bề mặt mềm (chăn, gối) vì cản luồng khí.
• Kiểm tra hướng quạt trong case: đảm bảo luồng khí vào/ra hợp lý.
• Vệ sinh khe thông gió định kỳ.

✅ Kết quả mong đợi:
• Nhiệt độ CPU/GPU giảm 5-15°C.
• Hệ thống ổn định hơn khi hoạt động lâu dài.

💡 Mẹo:
• Sử dụng phần mềm như SpeedFan để kiểm tra tốc độ quạt.
• Cân nhắc lắp thêm quạt case nếu luồng khí yếu.";

                case "Giảm tải ứng dụng":
                    return @"📋 Mục đích:
Đóng các ứng dụng nặng để giảm nhiệt độ CPU và cải thiện hiệu năng tổng thể.

📝 Các bước thực hiện:
1. Nhấn Ctrl + Shift + Esc để mở Task Manager.
2. Chuyển sang tab 'Processes'.
3. Sắp xếp theo cột 'CPU' để tìm ứng dụng sử dụng CPU cao.
4. Chọn ứng dụng không cần thiết (ví dụ: game, trình chỉnh sửa video).
5. Nhấn 'End Task' để đóng ứng dụng.
6. Theo dõi nhiệt độ CPU trong System Monitor.

⚠️ Lưu ý:
• Không đóng các process hệ thống như winlogon, explorer.exe.
• Lưu công việc trước khi đóng ứng dụng.
• Một số ứng dụng có thể tự khởi động lại.

✅ Kết quả mong đợi:
• Nhiệt độ CPU giảm 5-20°C ngay lập tức.
• Hệ thống phản hồi nhanh hơn.

💡 Mẹo:
• Kiểm tra tab 'Process Monitor' trong System Monitor để xác định ứng dụng nặng.
• Tắt tính năng tự khởi động của ứng dụng trong cài đặt của nó.";

                // RAM Solutions
                case "Tìm Process tốn RAM":
                    return @"📋 Mục đích:
Xác định và đóng các process sử dụng RAM cao để giải phóng bộ nhớ và cải thiện hiệu năng.

📝 Các bước thực hiện:
1. Mở ứng dụng System Monitor.
2. Chuyển sang tab 'Process Monitor'.
3. Nhấn 'Refresh' để cập nhật danh sách process.
4. Sắp xếp theo cột 'Memory' (từ cao xuống thấp).
5. Tìm process sử dụng RAM > 1GB mà không cần thiết (ví dụ: trình duyệt có nhiều tab).
6. Right-click và chọn 'End Process'.
7. Theo dõi RAM usage trong tab 'System Status'.

⚠️ Lưu ý:
• Không đóng process hệ thống như System, svchost.exe.
• Lưu công việc trong trình duyệt trước khi đóng.
• Một số process có thể khởi động lại tự động.

✅ Kết quả mong đợi:
• Giảm RAM usage 1-4GB tùy thuộc vào process.
• Hệ thống mượt mà hơn, đặc biệt khi đa nhiệm.

💡 Mẹo:
• Đóng các tab trình duyệt không cần thiết trước khi end process.
• Sử dụng Task Manager để kiểm tra RAM usage theo thời gian thực.";

                case "Giới hạn tài nguyên":
                    return @"📋 Mục đích:
Đặt giới hạn CPU/RAM cho process để ngăn chúng chiếm quá nhiều tài nguyên, đảm bảo hệ thống ổn định.

📝 Các bước thực hiện:
1. Mở ứng dụng System Monitor.
2. Chuyển sang tab 'Process Monitor'.
3. Tìm process sử dụng CPU hoặc RAM cao.
4. Right-click và chọn 'Set Resource Limit'.
5. Đặt giới hạn:
   • CPU: 30-50% cho ứng dụng thông thường.
   • RAM: 1-2GB cho ứng dụng nhẹ, 3-6GB cho ứng dụng nặng.
6. Nhấn 'Apply' và theo dõi hiệu quả trong tab 'System Status'.

⚙️ Mức giới hạn khuyến nghị:
• Browser: CPU 40-60%, RAM 2-4GB.
• Office apps: CPU 20-30%, RAM 500MB-1GB.
• Games: CPU 70-80%, RAM 6-12GB.

⚠️ Lưu ý:
• Giới hạn quá thấp có thể làm ứng dụng crash.
• Kiểm tra hiệu năng sau khi đặt giới hạn.
• Có thể bỏ giới hạn bằng cách chọn 'Remove Limit'.

✅ Kết quả mong đợi:
• Hệ thống ổn định hơn, không bị treo do process chiếm tài nguyên.
• RAM usage giảm đáng kể.

💡 Mẹo:
• Ưu tiên giới hạn RAM cho trình duyệt hoặc ứng dụng đa nhiệm.
• Sử dụng Resource Monitor của Windows để kiểm tra chi tiết.";

                case "Dọn dẹp hệ thống":
                    return @"📋 Mục đích:
Xóa file tạm, bộ nhớ cache và khởi động lại trình duyệt để giải phóng RAM và cải thiện hiệu năng.

📝 Các bước thực hiện:
1. Nhấn Windows + R, gõ 'cleanmgr' và nhấn Enter.
2. Chọn ổ đĩa hệ thống (thường là C:).
3. Chọn các mục như 'Temporary files', 'Recycle Bin', 'Thumbnails'.
4. Nhấn 'OK' để xóa file rác.
5. Đóng và khởi động lại tất cả trình duyệt (Chrome, Firefox, v.v.).
6. Theo dõi RAM usage trong System Monitor.

⚠️ Lưu ý:
• Đảm bảo không xóa file quan trọng trong 'Downloads' hoặc 'Documents'.
• Sao lưu dữ liệu nếu không chắc chắn.
• Khởi động lại trình duyệt có thể đóng các tab đang mở.

✅ Kết quả mong đợi:
• Giải phóng 500MB-2GB RAM.
• Hệ thống phản hồi nhanh hơn.

💡 Mẹo:
• Chạy Disk Cleanup định kỳ (1-2 tháng/lần).
• Sử dụng CCleaner để dọn dẹp sâu hơn nếu cần.";

                case "Tăng Virtual Memory":
                    return @"📋 Mục đích:
Tăng dung lượng bộ nhớ ảo để hỗ trợ RAM khi bộ nhớ vật lý bị sử dụng hết, giảm tình trạng thiếu RAM.

📝 Các bước thực hiện:
1. Nhấn Windows + S, gõ 'Advanced System Settings' và mở.
2. Trong tab 'Advanced', mục 'Performance', nhấn 'Settings'.
3. Chuyển sang tab 'Advanced' trong cửa sổ mới.
4. Trong mục 'Virtual Memory', nhấn 'Change'.
5. Bỏ chọn 'Automatically manage paging file size'.
6. Chọn ổ đĩa hệ thống (thường là C:).
7. Chọn 'Custom size' và đặt:
   • Initial size: 1.5x dung lượng RAM (ví dụ: 8GB RAM → 12000MB).
   • Maximum size: 3x dung lượng RAM (ví dụ: 8GB RAM → 24000MB).
8. Nhấn 'Set', sau đó 'OK' và khởi động lại máy.

⚠️ Lưu ý:
• Đảm bảo ổ C: có đủ dung lượng trống.
• Không đặt Virtual Memory quá lớn trên ổ SSD để tránh hao mòn.
• Khởi động lại máy là bắt buộc để áp dụng thay đổi.

✅ Kết quả mong đợi:
• Giảm tình trạng thiếu RAM khi chạy nhiều ứng dụng.
• Hệ thống ổn định hơn khi đa nhiệm.

💡 Mẹo:
• Kiểm tra RAM usage trước và sau khi thay đổi trong System Monitor.
• Nếu có nhiều RAM (>16GB), có thể không cần tăng Virtual Memory.";

                case "Kiểm tra Memory Leaks":
                    return @"📋 Mục đích:
Xác định ứng dụng gây rò rỉ bộ nhớ (memory leak) dẫn đến RAM usage tăng bất thường.

📝 Các bước thực hiện:
1. Nhấn Ctrl + Shift + Esc để mở Task Manager.
2. Chuyển sang tab 'Processes'.
3. Sắp xếp theo cột 'Memory' và theo dõi trong 5-10 phút.
4. Tìm process có RAM usage tăng dần mà không giảm.
5. Ghi lại tên process và tra cứu trên Google để xác định vấn đề.
6. Đóng process nếu an toàn hoặc cập nhật/phân tích thêm.

⚠️ Lưu ý:
• Memory leak thường xảy ra với trình duyệt hoặc ứng dụng lỗi thời.
• Không đóng process hệ thống như svchost.exe.
• Sao lưu công việc trước khi đóng ứng dụng.

✅ Kết quả mong đợi:
• Phát hiện và khắc phục process gây rò rỉ RAM.
• Giảm RAM usage 1-3GB.

💡 Mẹo:
• Cập nhật ứng dụng nghi ngờ memory leak lên phiên bản mới nhất.
• Sử dụng Resource Monitor để xem chi tiết memory usage.";

                // GPU Solutions
                case "Tối ưu Power Plan":
                    return @"📋 Mục đích:
Chuyển sang Power Plan phù hợp để giảm tải GPU, đặc biệt khi GPU usage cao hoặc chơi game.

📝 Các bước thực hiện:
1. Mở ứng dụng System Monitor.
2. Chuyển sang tab 'Power Settings'.
3. Tìm dropdown 'Power Plan'.
4. Chọn 'Balanced' hoặc 'Power Saver' để giảm GPU load.
5. Nhấn 'Apply' và theo dõi GPU usage trong tab 'System Status'.

⚠️ Lưu ý:
• Power Saver có thể làm giảm FPS khi chơi game.
• Chuyển lại 'High Performance' khi cần hiệu năng GPU tối đa.
• Đảm bảo driver GPU được cập nhật.

✅ Kết quả mong đợi:
• Giảm GPU usage 10-20%.
• Nhiệt độ GPU giảm 5-10°C.

💡 Mẹo:
• Kết hợp với giải pháp 'Giảm hiệu năng' để tối ưu hóa.
• Sử dụng NVIDIA Control Panel hoặc AMD Radeon Software để tinh chỉnh thêm.";

                case "Kiểm tra Process GPU":
                    return @"📋 Mục đích:
Xác định và đóng các process sử dụng GPU cao để giảm tải và nhiệt độ GPU.

📝 Các bước thực hiện:
1. Mở ứng dụng System Monitor.
2. Chuyển sang tab 'Process Monitor'.
3. Nhấn 'Refresh' và sắp xếp theo cột 'GPU%'.
4. Tìm process sử dụng GPU > 20% mà không cần thiết (ví dụ: game, trình chỉnh sửa video).
5. Right-click và chọn 'End Process'.
6. Theo dõi GPU usage trong tab 'System Status'.

⚠️ Lưu ý:
• Không đóng process hệ thống hoặc driver GPU (như NVIDIA Container).
• Lưu công việc trước khi đóng ứng dụng.
• Một số process có thể tự khởi động lại.

✅ Kết quả mong đợi:
• Giảm GPU usage 20-50%.
• Nhiệt độ GPU giảm 5-15°C.

💡 Mẹo:
• Sử dụng Task Manager hoặc GPU-Z để kiểm tra GPU usage chi tiết.
• Đóng tab trình duyệt chạy video hoặc WebGL.";

                case "Giảm hiệu năng":
                    return @"📋 Mục đích:
Giảm cài đặt đồ họa trong game hoặc ứng dụng để giảm tải GPU và nhiệt độ.

📝 Các bước thực hiện:
1. Mở game hoặc ứng dụng sử dụng GPU cao.
2. Vào menu 'Settings' hoặc 'Graphics Options'.
3. Giảm các cài đặt:
   • Resolution: Từ 4K xuống 1080p hoặc 720p.
   • Texture Quality: Từ High xuống Medium/Low.
   • Tắt các hiệu ứng: Anti-aliasing, Shadows, Ray Tracing.
4. Lưu cài đặt và khởi động lại game.
5. Theo dõi GPU usage và nhiệt độ trong System Monitor.

⚠️ Lưu ý:
• Giảm quá nhiều có thể làm giảm trải nghiệm hình ảnh.
• Ghi lại cài đặt gốc để khôi phục nếu cần.
• Kiểm tra driver GPU để đảm bảo hiệu năng tối ưu.

✅ Kết quả mong đợi:
• Giảm GPU usage 20-40%.
• Nhiệt độ GPU giảm 10-20°C.

💡 Mẹo:
• Sử dụng NVIDIA GeForce Experience để tự động tối ưu cài đặt game.
• Kiểm tra FPS bằng công cụ như Fraps hoặc Steam Overlay.";

                case "Cập nhật Driver GPU":
                    return @"📋 Mục đích:
Cài đặt phiên bản driver GPU mới nhất để cải thiện hiệu năng và giảm lỗi liên quan đến GPU.

📝 Các bước thực hiện:
1. Nhấn Windows + X, chọn 'Device Manager'.
2. Mở rộng mục 'Display Adapters'.
3. Right-click trên GPU (ví dụ: NVIDIA GeForce, AMD Radeon).
4. Chọn 'Update driver'.
5. Chọn 'Search automatically for drivers'.
6. Nếu không tìm thấy bản cập nhật, tải driver từ:
   • NVIDIA: www.nvidia.com/Download
   • AMD: www.amd.com/support
7. Cài đặt driver và khởi động lại máy.

⚠️ Lưu ý:
• Sao lưu hệ thống trước khi cập nhật driver.
• Đảm bảo tải driver từ nguồn chính thức.
• Gỡ driver cũ bằng DDU (Display Driver Uninstaller) nếu gặp lỗi.

✅ Kết quả mong đợi:
• Giảm GPU usage và lỗi đồ họa.
• Cải thiện hiệu năng trong game và ứng dụng.

💡 Mẹo:
• Sử dụng NVIDIA GeForce Experience hoặc AMD Radeon Software để tự động cập nhật.
• Kiểm tra GPU usage sau khi cập nhật trong System Monitor.";

                case "Tối ưu Game Settings":
                    return @"📋 Mục đích:
Tinh chỉnh cài đặt game để giảm tải GPU, cải thiện FPS và giảm nhiệt độ.

📝 Các bước thực hiện:
1. Mở game và vào menu 'Settings' hoặc 'Graphics'.
2. Chọn preset 'Low Performance Mode' nếu có.
3. Nếu không có preset, điều chỉnh:
   • Resolution: Giảm xuống 1080p hoặc 720p.
   • Texture Quality: Medium hoặc Low.
   • Tắt Shadows, Anti-aliasing, Ray Tracing.
4. Lưu cài đặt và khởi động lại game.
5. Theo dõi GPU usage trong System Monitor.

⚠️ Lưu ý:
• Một số game yêu cầu khởi động lại để áp dụng cài đặt.
• Ghi lại cài đặt gốc để khôi phục nếu cần.
• Đảm bảo driver GPU được cập nhật.

✅ Kết quả mong đợi:
• Tăng FPS 20-50%.
• Giảm GPU usage và nhiệt độ 10-20°C.

💡 Mẹo:
• Sử dụng NVIDIA GeForce Experience để tối ưu tự động.
• Kiểm tra nhiệt độ GPU bằng GPU-Z sau khi thay đổi.";

                // GPU Temp Solutions
                case "Power Management":
                    return @"📋 Mục đích:
Giảm power limit của GPU để hạ nhiệt độ và tiêu thụ điện năng.

📝 Các bước thực hiện:
1. Mở ứng dụng System Monitor.
2. Chuyển sang tab 'Power Settings'.
3. Tìm mục 'GPU Power Limit'.
4. Giảm xuống 70-80% (khuyến nghị 75%).
5. Nhấn 'Apply' và theo dõi nhiệt độ GPU trong tab 'System Status'.

⚠️ Lưu ý:
• Giảm power limit có thể làm giảm FPS trong game.
• Không đặt dưới 60% để tránh lỗi hiển thị.
• Kết hợp với giải pháp 'Tăng tốc quạt' nếu cần.

✅ Kết quả mong đợi:
• Nhiệt độ GPU giảm 5-15°C.
• Tiết kiệm năng lượng 10-20%.

💡 Mẹo:
• Sử dụng MSI Afterburner để tinh chỉnh power limit chi tiết hơn.
• Kiểm tra nhiệt độ GPU bằng GPU-Z.";

                case "Tăng tốc quạt":
                    return @"📋 Mục đích:
Tăng tốc độ quạt GPU để cải thiện tản nhiệt, giảm nhiệt độ GPU.

📝 Các bước thực hiện:
1. Tải và cài đặt MSI Afterburner (www.msi.com/afterburner).
2. Mở MSI Afterburner và tìm mục 'Fan Speed'.
3. Chuyển sang chế độ 'Manual' hoặc 'Custom Fan Curve'.
4. Tăng tốc độ quạt lên 70-80% khi GPU nóng (>80°C).
5. Nhấn 'Apply' và theo dõi nhiệt độ GPU.
6. Lưu fan curve để tự động áp dụng.

⚠️ Lưu ý:
• Tăng tốc quạt có thể gây tiếng ồn lớn hơn.
• Không đặt tốc độ quạt 100% liên tục để tránh hao mòn.
• Đảm bảo quạt sạch, không bị bụi bám.

✅ Kết quả mong đợi:
• Nhiệt độ GPU giảm 10-20°C.
• Hiệu năng GPU ổn định hơn.

💡 Mẹo:
• Kết hợp với giải pháp 'Vệ sinh làm mát' để tối ưu.
• Sử dụng GPU-Z để kiểm tra nhiệt độ và tốc độ quạt.";

                case "Vệ sinh làm mát":
                    return @"📋 Mục đích:
Vệ sinh quạt và tản nhiệt GPU để cải thiện hiệu quả làm mát, giảm nhiệt độ.

📝 Các bước thực hiện:
1. Tắt máy tính và rút nguồn điện.
2. Mở case và xác định vị trí GPU.
3. Sử dụng bình khí nén để thổi bụi khỏi quạt GPU và tản nhiệt.
4. Kiểm tra keo tản nhiệt GPU nếu cần (thay sau 1-2 năm).
5. Lắp lại case và khởi động máy.
6. Theo dõi nhiệt độ GPU trong System Monitor.

⚠️ Lưu ý:
• Ngắt nguồn điện trước khi vệ sinh.
• Sử dụng khí nén đúng cách, tránh làm hỏng linh kiện.
• Nếu không quen, liên hệ kỹ thuật viên.

✅ Kết quả mong đợi:
• Nhiệt độ GPU giảm 10-30°C.
• Giảm tiếng ồn quạt và cải thiện hiệu năng.

💡 Mẹo:
• Vệ sinh định kỳ 6 tháng/lần.
• Sử dụng keo tản nhiệt chất lượng (như Arctic MX-4).";

                case "Kiểm tra Nhiệt độ":
                    return @"📋 Mục đích:
Theo dõi nhiệt độ GPU trong thời gian thực để phát hiện vấn đề và áp dụng giải pháp kịp thời.

📝 Các bước thực hiện:
1. Tải và cài đặt GPU-Z (www.techpowerup.com/gpuz) hoặc MSI Afterburner.
2. Mở phần mềm và tìm mục 'Temperature' hoặc 'Sensors'.
3. Theo dõi nhiệt độ GPU khi chạy ứng dụng nặng (game, render).
4. Nếu nhiệt độ > 80°C, áp dụng giải pháp như 'Tăng tốc quạt' hoặc 'Vệ sinh làm mát'.
5. Kiểm tra lại nhiệt độ trong System Monitor.

⚠️ Lưu ý:
• Nhiệt độ GPU > 85°C kéo dài có thể gây hại.
• Đảm bảo phần mềm theo dõi được cập nhật.
• Kiểm tra nhiệt độ cả khi idle và dưới tải.

✅ Kết quả mong đợi:
• Phát hiện sớm vấn đề nhiệt độ GPU.
• Hỗ trợ áp dụng giải pháp phù hợp.

💡 Mẹo:
• Thiết lập cảnh báo nhiệt độ trong MSI Afterburner.
• Kết hợp với giải pháp 'Tăng luồng gió' để tối ưu.";

                case "Undervolt GPU":
                    return @"📋 Mục đích:
Giảm điện áp GPU để hạ nhiệt độ và tiết kiệm năng lượng mà không ảnh hưởng lớn đến hiệu năng.

📝 Các bước thực hiện:
1. Tải và cài đặt MSI Afterburner.
2. Mở MSI Afterburner và nhấn Ctrl + F để mở Curve Editor.
3. Tìm đường cong điện áp/tần số (Voltage/Frequency Curve).
4. Giảm điện áp (mV) xuống 50-100mV cho tần số hiện tại.
5. Nhấn 'Apply' và kiểm tra ổn định bằng game hoặc benchmark.
6. Nếu ổn định, lưu profile; nếu crash, tăng điện áp lên 10mV và thử lại.

⚠️ Lưu ý:
• Undervolt không đúng có thể gây crash hệ thống.
• Sao lưu profile gốc trong MSI Afterburner.
• Thử nghiệm từng bước nhỏ và kiểm tra ổn định.

✅ Kết quả mong đợi:
• Nhiệt độ GPU giảm 5-15°C.
• Tiết kiệm năng lượng 10-20%.

💡 Mẹo:
• Sử dụng Heaven Benchmark để kiểm tra ổn định sau undervolt.
• Tham khảo hướng dẫn undervolt cho dòng GPU cụ thể (NVIDIA/AMD).";

                // Disk Solutions
                case "Kiểm tra Disk Activity":
                    return @"📋 Mục đích:
Xác định process gây sử dụng disk cao để tạm dừng hoặc đóng, giảm tải hệ thống.

📝 Các bước thực hiện:
1. Mở ứng dụng System Monitor.
2. Chuyển sang tab 'Process Monitor'.
3. Nhấn 'Refresh' và sắp xếp theo cột 'Disk I/O'.
4. Tìm process sử dụng disk > 20% (ví dụ: antivirus, backup).
5. Right-click và chọn 'Pause' hoặc 'End Process' nếu an toàn.
6. Theo dõi disk usage trong tab 'System Status'.

⚠️ Lưu ý:
• Không đóng process hệ thống như Windows Update, svchost.exe.
• Lưu công việc trước khi đóng ứng dụng.
• Một số process như backup có thể tự khởi động lại.

✅ Kết quả mong đợi:
• Giảm disk usage 20-50%.
• Hệ thống phản hồi nhanh hơn.

💡 Mẹo:
• Kiểm tra Resource Monitor của Windows để xem chi tiết disk I/O.
• Tạm dừng Windows Update nếu gây disk usage cao.";

                case "Giới hạn I/O":
                    return @"📋 Mục đích:
Đặt giới hạn disk usage cho process để ngăn chúng chiếm quá nhiều tài nguyên đĩa.

📝 Các bước thực hiện:
1. Mở ứng dụng System Monitor.
2. Chuyển sang tab 'Process Monitor'.
3. Tìm process sử dụng disk I/O cao.
4. Right-click và chọn 'Set Resource Limit'.
5. Đặt giới hạn disk I/O (ví dụ: 10-20MB/s cho ứng dụng thông thường).
6. Nhấn 'Apply' và theo dõi disk usage trong tab 'System Status'.

⚠️ Lưu ý:
• Giới hạn quá thấp có thể làm chậm ứng dụng.
• Kiểm tra hiệu quả sau khi đặt giới hạn.
• Có thể bỏ giới hạn bằng 'Remove Limit'.

✅ Kết quả mong đợi:
• Giảm disk usage và cải thiện tốc độ hệ thống.
• Ổn định hơn khi chạy nhiều ứng dụng.

💡 Mẹo:
• Ưu tiên giới hạn I/O cho backup hoặc antivirus.
• Sử dụng Resource Monitor để kiểm tra disk activity chi tiết.";

                case "Tạm dừng backup/scan":
                    return @"📋 Mục đích:
Tạm dừng các tác vụ backup hoặc quét antivirus để giảm disk usage và cải thiện hiệu năng.

📝 Các bước thực hiện:
1. Kiểm tra System Tray (góc dưới bên phải) để tìm icon của Windows Defender, OneDrive hoặc phần mềm backup.
2. Right-click trên icon và chọn 'Pause' hoặc 'Suspend'.
3. Nếu sử dụng Windows Defender:
   • Mở Windows Security.
   • Chọn 'Virus & threat protection'.
   • Nhấn 'Manage settings' và tắt 'Real-time protection' tạm thời.
4. Theo dõi disk usage trong System Monitor.

⚠️ Lưu ý:
• Không tắt antivirus quá lâu để tránh rủi ro bảo mật.
• Kiểm tra lại backup/scan sau khi hoàn thành công việc quan trọng.
• Đảm bảo bật lại 'Real-time protection' sau khi tạm dừng.

✅ Kết quả mong đợi:
• Giảm disk usage 20-50%.
• Hệ thống phản hồi nhanh hơn ngay lập tức.

💡 Mẹo:
• Lên lịch quét antivirus vào thời điểm ít sử dụng máy.
• Tắt tự động sync của OneDrive khi không cần.";

                case "Disk Sleep Settings":
                    return @"📋 Mục đích:
Tối ưu cài đặt chế độ ngủ của ổ đĩa để giảm disk usage và tiết kiệm năng lượng.

📝 Các bước thực hiện:
1. Mở ứng dụng System Monitor.
2. Chuyển sang tab 'Power Settings'.
3. Tìm mục 'Disk Sleep Settings'.
4. Đặt thời gian ngủ (sleep) cho ổ đĩa (ví dụ: 5-10 phút khi không hoạt động).
5. Nhấn 'Apply' và theo dõi disk usage trong tab 'System Status'.

⚠️ Lưu ý:
• Disk Sleep có thể làm chậm truy cập ổ đĩa khi khởi động lại.
• Không áp dụng cho ổ SSD vì chúng không cần chế độ ngủ.
• Kiểm tra hiệu quả trên ổ HDD.

✅ Kết quả mong đợi:
• Giảm disk usage khi hệ thống idle.
• Tiết kiệm năng lượng trên máy tính để bàn.

💡 Mẹo:
• Kết hợp với giải pháp 'Tạm dừng backup/scan' để tối ưu.
• Sử dụng Power Options trong Control Panel để tinh chỉnh thêm.";

                case "Dọn dẹp ổ đĩa":
                    return @"📋 Mục đích:
Xóa file rác và tối ưu hóa ổ đĩa để giảm disk usage và cải thiện tốc độ truy cập.

📝 Các bước thực hiện:
1. Nhấn Windows + R, gõ 'cleanmgr' và nhấn Enter.
2. Chọn ổ đĩa hệ thống (thường là C:).
3. Chọn các mục như 'Temporary files', 'Recycle Bin'.
4. Nhấn 'OK' để xóa file rác.
5. Mở 'Defragment and Optimize Drives' (tìm trong Windows Search).
6. Chọn ổ đĩa và nhấn 'Optimize' (chỉ áp dụng cho HDD).
7. Theo dõi disk usage trong System Monitor.

⚠️ Lưu ý:
• Không xóa file trong thư mục hệ thống (Windows, Program Files).
• Sao lưu dữ liệu quan trọng trước khi dọn dẹp.
• Không cần defrag cho SSD.

✅ Kết quả mong đợi:
• Giải phóng 1-10GB dung lượng ổ đĩa.
• Cải thiện tốc độ truy cập ổ đĩa.

💡 Mẹo:
• Chạy Disk Cleanup định kỳ (1-2 tháng/lần).
• Sử dụng CCleaner để dọn dẹp sâu hơn.";

                case "Kiểm tra sức khỏe ổ đĩa":
                    return @"📋 Mục đích:
Kiểm tra lỗi và tình trạng ổ đĩa để đảm bảo hoạt động ổn định, giảm disk usage bất thường.

📝 Các bước thực hiện:
1. Nhấn Windows + E để mở File Explorer.
2. Right-click trên ổ đĩa (thường là C:) và chọn 'Properties'.
3. Chuyển sang tab 'Tools' và nhấn 'Check' trong mục 'Error checking'.
4. Chọn 'Scan drive' và chờ hoàn tất.
5. Nếu phát hiện lỗi, làm theo hướng dẫn để sửa.
6. Theo dõi disk usage trong System Monitor.

⚠️ Lưu ý:
• Quá trình kiểm tra có thể mất 10-30 phút.
• Sao lưu dữ liệu quan trọng trước khi sửa lỗi.
• Nếu ổ đĩa có nhiều lỗi, cân nhắc thay thế.

✅ Kết quả mong đợi:
• Phát hiện và sửa lỗi ổ đĩa.
• Giảm disk usage do lỗi hệ thống.

💡 Mẹo:
• Sử dụng CrystalDiskInfo để kiểm tra tình trạng ổ đĩa chi tiết.
• Kiểm tra sức khỏe ổ đĩa định kỳ (3-6 tháng/lần).";

                default:
                    return $@"📋 Mô tả:
{solution.Description}

📝 Cách thực hiện:
{solution.Action}

⚠️ Lưu ý:
• Thực hiện từng bước cẩn thận.
• Theo dõi hiệu quả trong System Monitor sau khi áp dụng.
• Nếu không chắc chắn, tra cứu thêm thông tin hoặc liên hệ hỗ trợ.

✅ Kết quả mong đợi:
• Cải thiện hiệu năng hoặc giảm tải hệ thống.
• Hệ thống ổn định hơn.

💡 Mẹo:
• Kiểm tra tài nguyên hệ thống trước và sau khi áp dụng.
• Lưu lại cài đặt gốc để khôi phục nếu cần.";
            }
        }
    }
}
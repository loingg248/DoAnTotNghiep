using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public SolutionGuideWindow(WarningService.SolutionRecommendation solution)
        {
            InitializeComponent(); 
            SetupGuideContent(solution); 
        }

        private void SetupGuideContent(WarningService.SolutionRecommendation solution)
        {
            Title = $"Hướng dẫn: {solution.Title}";

            var content = GetDetailedGuide(solution);

            // Assuming we have a TextBlock named GuideContent in the XAML
            var guideTextBlock = FindName("GuideContent") as TextBlock;
            if (guideTextBlock != null)
            {
                guideTextBlock.Text = content;
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
                case "Bật Smart Mode":
                    return @"🤖 HƯỚNG DẪN: BẬT SMART MODE

📋 Mục đích:
Smart Mode sẽ tự động điều chỉnh CPU frequency dựa trên mức độ sử dụng thực tế, giúp:
• Giảm nhiệt độ CPU khi không cần hiệu năng cao
• Tiết kiệm pin trên laptop
• Giảm tiếng ồn quạt tản nhiệt

📝 Các bước thực hiện:
1. Chuyển sang tab 'Power Settings'
2. Tìm nút 'Enable Smart Mode' 
3. Click để bật tính năng
4. Quan sát CPU frequency sẽ thay đổi theo tải

⚠️ Lưu ý:
• Smart Mode có thể làm giảm hiệu năng khi cần xử lý nặng
• Có thể tắt bất cứ lúc nào bằng nút 'Disable Smart Mode'
• Phù hợp cho công việc văn phòng, duyệt web

✅ Kết quả mong đợi:
CPU usage và nhiệt độ sẽ giảm trong vòng 30 giây - 1 phút.";

                case "Chuyển Power Plan":
                    return @"⚡ HƯỚNG DẪN: CHUYỂN POWER PLAN

📋 Các loại Power Plan:
• High Performance: Hiệu năng cao nhất, tốn pin nhiều
• Balanced: Cân bằng giữa hiệu năng và tiết kiệm pin
• Power Saver: Tiết kiệm pin tối đa, hiệu năng thấp

📝 Các bước thực hiện:
1. Chuyển sang tab 'Power Settings'
2. Tìm dropdown 'Power Plan'
3. Chọn 'Power Saver' để giảm CPU load
4. Hoặc chọn 'Balanced' cho mức trung bình

⚠️ Khi nào nên chuyển:
• CPU usage > 80% liên tục → Power Saver
• Nhiệt độ CPU > 80°C → Power Saver
• Làm việc văn phòng → Balanced
• Gaming/Rendering → High Performance

✅ Hiệu quả:
• Power Saver: Giảm 20-30% CPU usage
• Giảm nhiệt độ 5-10°C
• Tăng thời lượng pin 30-50%";

                case "Giảm CPU Frequency":
                    return @"🔧 HƯỚNG DẪN: GIẢM CPU FREQUENCY

📋 CPU Frequency là gì:
• Tốc độ xung nhịp của CPU (đo bằng GHz)
• Frequency cao = hiệu năng cao = nhiệt độ cao
• Giảm frequency = hiệu năng thấp hơn nhưng mát hơn

📝 Các bước thực hiện:
1. Chuyển sang tab 'Power Settings'
2. Tìm ô 'Max CPU Frequency'
3. Nhập giá trị 60-80% (khuyến nghị 70%)
4. Click nút 'Set' để áp dụng

⚙️ Mức độ khuyến nghị:
• CPU quá nóng (>85°C): 60-65%
• CPU hơi nóng (75-85°C): 70-75%
• CPU bình thường (<75°C): 80-90%

⚠️ Lưu ý quan trọng:
• Quá thấp (<50%) có thể làm máy chậm
• Test từng mức 5% một để tìm mức phù hợp
• Có thể reset về 100% bất cứ lúc nào

✅ Kết quả:
Nhiệt độ CPU giảm 10-20°C trong 2-3 phút.";

                case "Quản lý Process":
                    return @"🔄 HƯỚNG DẪN: QUẢN LÝ PROCESS

📋 Process là gì:
• Các chương trình đang chạy trên máy
• Mỗi process tiêu thụ CPU, RAM, Disk
• Đóng process không cần thiết sẽ giảm tải hệ thống

📝 Các bước thực hiện:
1. Chuyển sang tab 'Process Monitor'
2. Click 'Refresh' để cập nhật danh sách
3. Sắp xếp theo cột 'CPU%' (cao xuống thấp)
4. Tìm process có CPU% > 20% mà không cần thiết
5. Right-click → 'End Process'

⚠️ CẢNH BÁO - KHÔNG đóng các process này:
• System, System Idle Process
• Windows processes (winlogon, csrss, etc.)
• Antivirus software
• Drivers (*.exe trong System32)

✅ An toàn khi đóng:
• Browsers (chrome.exe, firefox.exe)
• Games không đang chơi
• Applications không sử dụng
• Background updaters

🔍 Dấu hiệu process có vấn đề:
• CPU% > 50% trong thời gian dài
• Memory usage > 1GB cho app đơn giản
• Responding = 'Not Responding'";

                case "Giới hạn tài nguyên":
                    return @"⚠️ HƯỚNG DẪN: GIỚI HẠN TÀI NGUYÊN

📋 Tại sao cần giới hạn:
• Ngăn 1 process chiếm hết CPU/RAM
• Đảm bảo hệ thống vẫn phản hồi
• Ưu tiên tài nguyên cho app quan trọng

📝 Các bước thực hiện:
1. Chuyển sang tab 'Process Monitor'
2. Tìm process cần giới hạn
3. Right-click → 'Set Resource Limit'
4. Đặt giới hạn phù hợp:
   • CPU: 30-50% cho app thường
   • Memory: 1-2GB cho app thường

⚙️ Mức giới hạn khuyến nghị:

CPU Limits:
• Browser: 40-60%
• Office apps: 20-30%  
• Media players: 30-50%
• Games: 70-80%

Memory Limits:
• Light apps: 500MB-1GB
• Browsers: 2-4GB
• IDEs: 3-6GB
• Games: 6-12GB

⚠️ Lưu ý:
• Giới hạn quá thấp có thể làm app crash
• Monitor hiệu quả sau khi set limit
• Có thể điều chỉnh lại nếu cần

✅ Hiệu quả:
Process không thể vượt quá giới hạn đã đặt, hệ thống ổn định hơn.";

                default:
                    return $@"📖 HƯỚNG DẪN: {solution.Title}

📋 Mô tả:
{solution.Description}

📝 Cách thực hiện:
{solution.Action}

💡 Mẹo:
• Thực hiện từng bước một cách cẩn thận
• Monitor kết quả sau khi thực hiện
• Có thể revert lại nếu không hiệu quả

⚠️ Lưu ý:
Nếu không chắc chắn, hãy tìm hiểu thêm trước khi thực hiện.";
            }
        }
    }
}
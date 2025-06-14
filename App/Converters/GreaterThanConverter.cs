using System;
using System.Globalization;
using System.Windows.Data;

namespace SystemMonitor.Converters
{
    // Converter để kiểm tra giá trị lớn hơn threshold
    public class GreaterThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            if (double.TryParse(value.ToString(), out double numValue) &&
                double.TryParse(parameter.ToString(), out double threshold))
            {
                return numValue > threshold;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Converter để kiểm tra giá trị trong khoảng
    public class BetweenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            if (double.TryParse(value.ToString(), out double numValue))
            {
                var range = parameter.ToString().Split(',');
                if (range.Length == 2 &&
                    double.TryParse(range[0], out double min) &&
                    double.TryParse(range[1], out double max))
                {
                    return numValue >= min && numValue <= max;
                }
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Converter để chuyển đổi trạng thái thành màu
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "Gray";

            string status = value.ToString();
            return status switch
            {
                "Đang chạy" => "LightGreen",
                "Không phản hồi" => "Orange",
                "Đã thoát" => "Red",
                _ => "Gray"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
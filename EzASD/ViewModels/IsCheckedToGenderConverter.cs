using System;
using System.Globalization;
using System.Windows.Data;

namespace EzASD.ViewModels
{
    public class IsCheckedToGenderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string gender)
            {
                return gender switch
                {
                    "男" => true,
                    "女" => false,
                    _ => throw new InvalidCastException(),
                }; ;
            }

            throw new InvalidCastException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked)
            {
                return isChecked ? "男" : "女";
            }

            return "未知";
        }
    }
}

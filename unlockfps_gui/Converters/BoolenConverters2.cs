using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnlockFps.Gui.Converters
{
    public class BoolenConverters2 : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            // 逻辑：两个都为true时返回true
            return values.Count >= 2 &&
                   values[0] is bool b1 && b1 &&
                   values[1] is bool b2 && b2;
        }

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            // 当开关改变时，同时设置两个属性
            bool isChecked = value is bool b && b;
            return new object?[] { isChecked, isChecked };
        }
    }
}

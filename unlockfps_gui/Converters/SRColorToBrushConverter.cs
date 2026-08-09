using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnlockFps.Gui.Converters
{
    internal sealed class SRColorToBrushConverter : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 4)
                return AvaloniaProperty.UnsetValue;

            float ToFloat(object? v)
            {
                if (v is float f) return f;
                if (v is double d) return (float)d;
                if (v is decimal m) return (float)m;
                if (v is int i) return i;
                if (v is string s && float.TryParse(s, out var r)) return r;
                return 0f;
            }

            var r = ToFloat(values[0]);
            var g = ToFloat(values[1]);
            var b = ToFloat(values[2]);
            var a = ToFloat(values[3]);

            byte ba = (byte)Math.Clamp((int)Math.Round(a * 255f), 0, 255);
            byte br = (byte)Math.Clamp((int)Math.Round(r * 255f), 0, 255);
            byte bg = (byte)Math.Clamp((int)Math.Round(g * 255f), 0, 255);
            byte bb = (byte)Math.Clamp((int)Math.Round(b * 255f), 0, 255);

            return new SolidColorBrush(Color.FromArgb(ba, br, bg, bb));
        }

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

using System.Globalization;
using MyHotsInfo.Extensions;

namespace MyHotsInfo.Utils;

public class HeroToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        var suff = parameter is string { } s ? $"_{s}" : "";
        var rc = value is not string { } k ? null : $"portrait_{k.Strip()}{suff}.png";
        return rc;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

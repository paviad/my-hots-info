using System.Globalization;
using MyHotsInfo.Extensions;

namespace MyHotsInfo;

public class HeroToImageConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        var rc = value is not string { } k ? null : $"portrait_{k.Strip()}_circle.png";
        return rc;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}

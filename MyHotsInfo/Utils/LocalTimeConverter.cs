using System.Globalization;

namespace MyHotsInfo.Utils;

/// <summary>Shows a stored UTC time in the machine's time zone.</summary>
public class LocalTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Times read from the database carry no kind; ToLocalTime takes those as UTC, which they are.
        return value switch {
            DateTime t => t.ToLocalTime().ToString(),
            DateTimeOffset t => t.ToLocalTime().DateTime.ToString(),
            _ => null,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

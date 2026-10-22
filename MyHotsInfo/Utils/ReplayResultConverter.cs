using System.Globalization;
using MyReplayLibrary.Data.Models;

namespace MyHotsInfo.Utils;

/// <summary>
/// Whether the game (a <see cref="ReplayEntry"/>) was the result named by the parameter,
/// "Victory" or "Defeat", for the user. Neither, for a game the user wasn't in.
/// </summary>
public class ReplayResultConverter : IValueConverter {
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        var won = (value as ReplayEntry)?.ReplayCharacters.FirstOrDefault(r => r.IsMe)?.IsWinner;
        return won is { } w && w == (parameter as string == "Victory");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}

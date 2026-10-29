using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace Continuum.ViewModels;

public static class Converters
{
    public static readonly IValueConverter MineToAlignment =
        new FuncValueConverter<bool, HorizontalAlignment>(mine => mine ? HorizontalAlignment.Right : HorizontalAlignment.Left);

    // Status bar is 44 px tall and the home indicator 22 px (see MainWindow).
    public static readonly IValueConverter HomeToMargin =
        new FuncValueConverter<bool, Thickness>(home => home ? new Thickness(0) : new Thickness(0, 44, 0, 22));
}

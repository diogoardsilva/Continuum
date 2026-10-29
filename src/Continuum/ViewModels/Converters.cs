using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace Continuum.ViewModels;

public static class Converters
{
    public static readonly IValueConverter MineToAlignment =
        new FuncValueConverter<bool, HorizontalAlignment>(mine => mine ? HorizontalAlignment.Right : HorizontalAlignment.Left);
}

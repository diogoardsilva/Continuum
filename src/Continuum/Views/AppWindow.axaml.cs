using Avalonia.Controls;

namespace Continuum.Views;

/// <summary>A Mac-style app window: translucent sidebar, content pane, system caption buttons.</summary>
public partial class AppWindow : Window
{
    public AppWindow()
    {
        InitializeComponent();
        // The app's view model is shown through the DataTemplates above.
        DataContextChanged += (_, _) => Content = DataContext;
    }
}

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Continuum.Views;

/// <summary>A Mac-style app window: translucent sidebar, content pane, system caption buttons.</summary>
public partial class AppWindow : Window
{
    public AppWindow()
    {
        InitializeComponent();
        // The app's view model is shown through the DataTemplates above.
        DataContextChanged += (_, _) => Content = DataContext;
        KeyDown += OnShortcut;
    }

    /// <summary>Ctrl+F jumps to the app's search field, Esc clears it, as in the Mac apps.</summary>
    void OnShortcut(object? sender, KeyEventArgs e)
    {
        var search = this.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.Classes.Contains("search") && t.IsEnabled);
        if (search is null) return;
        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
        {
            search.Focus();
            search.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && search.IsFocused && !string.IsNullOrEmpty(search.Text))
        {
            search.Text = "";
            e.Handled = true;
        }
    }
}

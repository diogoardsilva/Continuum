using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Continuum.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        UpdateClock();
        DispatcherTimer.Run(() => { UpdateClock(); return true; }, TimeSpan.FromSeconds(15));

        StatusBar.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        StatusBar.PointerEntered += (_, _) => WindowButtons.Opacity = 1;
        StatusBar.PointerExited += (_, _) => WindowButtons.Opacity = 0;
    }

    void UpdateClock() => Clock.Text = DateTime.Now.ToString("HH:mm");

    void MinimizeClicked(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    void CloseClicked(object? sender, RoutedEventArgs e) => Close();
}

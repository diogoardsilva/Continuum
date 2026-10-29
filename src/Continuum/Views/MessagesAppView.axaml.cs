using Avalonia.Controls;
using Avalonia.Threading;

namespace Continuum.Views;

public partial class MessagesAppView : UserControl
{
    public MessagesAppView()
    {
        InitializeComponent();
        // Every conversation opens at its newest message.
        Scroller.PropertyChanged += (_, e) =>
        {
            if (e.Property == ScrollViewer.ExtentProperty)
                Dispatcher.UIThread.Post(() => Scroller.ScrollToEnd(), DispatcherPriority.Background);
        };
    }
}

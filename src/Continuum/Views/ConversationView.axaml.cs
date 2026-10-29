using Avalonia.Controls;
using Avalonia.Threading;

namespace Continuum.Views;

public partial class ConversationView : UserControl
{
    public ConversationView()
    {
        InitializeComponent();
        // Open at the newest message, like the iPhone.
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => Scroller.ScrollToEnd(), DispatcherPriority.Loaded);
    }
}

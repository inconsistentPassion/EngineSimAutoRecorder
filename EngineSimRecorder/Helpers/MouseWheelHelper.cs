using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EngineSimRecorder.Helpers;

/// <summary>
/// Attached behavior to fix mouse wheel scrolling when child controls capture the event.
/// This forwards mouse wheel events from child controls to the parent ScrollViewer.
/// </summary>
public static class MouseWheelHelper
{
    public static readonly DependencyProperty EnableForwardingProperty =
        DependencyProperty.RegisterAttached(
            "EnableForwarding",
            typeof(bool),
            typeof(MouseWheelHelper),
            new PropertyMetadata(false, OnEnableForwardingChanged));

    public static bool GetEnableForwarding(DependencyObject obj) => (bool)obj.GetValue(EnableForwardingProperty);
    public static void SetEnableForwarding(DependencyObject obj, bool value) => obj.SetValue(EnableForwardingProperty, value);

    private static void OnEnableForwardingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element)
        {
            if ((bool)e.NewValue)
                element.PreviewMouseWheel += Element_PreviewMouseWheel;
            else
                element.PreviewMouseWheel -= Element_PreviewMouseWheel;
        }
    }

    private static void Element_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Only forward if we're not already at the ScrollViewer level
        if (sender is ScrollViewer)
            return;

        // Find the parent ScrollViewer
        var scrollViewer = FindParentScrollViewer(sender as DependencyObject);
        if (scrollViewer != null && scrollViewer.ScrollableHeight > 0)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }

    private static ScrollViewer? FindParentScrollViewer(DependencyObject? start)
    {
        while (start != null)
        {
            if (start is ScrollViewer sv)
                return sv;
            start = VisualTreeHelper.GetParent(start);
        }
        return null;
    }
}

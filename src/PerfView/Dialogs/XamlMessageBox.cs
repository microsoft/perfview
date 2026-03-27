using System.Windows;

namespace PerfView.Dialogs;

/// <summary>
///  Themed replacement for <see cref="MessageBox"/> that uses a custom XAML window.
/// </summary>
public static class XamlMessageBox
{
    /// <inheritdoc cref="MessageBox.Show(string)"/>
    public static MessageBoxResult Show(string message)
        => Show(null, message, string.Empty, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.OK);

    /// <inheritdoc cref="MessageBox.Show(string, string)"/>
    public static MessageBoxResult Show(string message, string caption)
        => Show(null, message, caption, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.OK);

    /// <inheritdoc cref="MessageBox.Show(string, string, MessageBoxButton)"/>
    public static MessageBoxResult Show(string message, string caption, MessageBoxButton buttons)
        => Show(null, message, caption, buttons, MessageBoxImage.None, MessageBoxResult.OK);

    /// <inheritdoc cref="MessageBox.Show(string, string, MessageBoxButton, MessageBoxImage)"/>
    public static MessageBoxResult Show(string message, string caption, MessageBoxButton buttons, MessageBoxImage icon)
        => Show(null, message, caption, buttons, icon, MessageBoxResult.OK);

    /// <inheritdoc cref="MessageBox.Show(string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult)"/>
    public static MessageBoxResult Show(string message, string caption, MessageBoxButton buttons, MessageBoxImage icon, MessageBoxResult defaultResult)
        => Show(null, message, caption, buttons, icon, defaultResult);

    /// <inheritdoc cref="MessageBox.Show(Window, string)"/>
    public static MessageBoxResult Show(Window owner, string message)
        => Show(owner, message, string.Empty, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.OK);

    /// <inheritdoc cref="MessageBox.Show(Window, string, string)"/>
    public static MessageBoxResult Show(Window owner, string message, string caption)
        => Show(owner, message, caption, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.OK);

    /// <inheritdoc cref="MessageBox.Show(Window, string, string, MessageBoxButton)"/>
    public static MessageBoxResult Show(Window owner, string message, string caption, MessageBoxButton buttons)
        => Show(owner, message, caption, buttons, MessageBoxImage.None, MessageBoxResult.OK);

    /// <inheritdoc cref="MessageBox.Show(Window, string, string, MessageBoxButton, MessageBoxImage)"/>
    public static MessageBoxResult Show(Window owner, string message, string caption, MessageBoxButton buttons, MessageBoxImage icon)
        => Show(owner, message, caption, buttons, icon, MessageBoxResult.OK);

    /// <inheritdoc cref="MessageBox.Show(Window, string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult)"/>
    public static MessageBoxResult Show(Window owner, string message, string caption, MessageBoxButton buttons, MessageBoxImage icon, MessageBoxResult defaultResult)
    {
        // XamlMessageBox uses a WPF window that must be created and shown on the UI thread.
        // Auto-dispatch to match the old System.Windows.MessageBox behavior of working from
        // any thread. This fixes callers like the SecurityCheck delegate which is invoked from
        // background threads during symbol resolution (see issue #2300).
        var dispatcher = owner?.Dispatcher ?? Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            return dispatcher.Invoke(() => Show(owner, message, caption, buttons, icon, defaultResult));
        }

        MessageBoxWindow window = new(message, caption, buttons, icon, defaultResult);
        if (owner is not null)
        {
            window.Owner = owner;
        }

        window.ShowDialog();
        return window.Result;
    }
}

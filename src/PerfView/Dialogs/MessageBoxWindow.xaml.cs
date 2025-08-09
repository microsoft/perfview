using System.Windows;
using System.Windows.Controls;

namespace PerfView.Dialogs;

/// <summary>
///  Simple themed message box window.
/// </summary>
internal partial class MessageBoxWindow : Window
{
    public MessageBoxResult Result { get; private set; }

    public MessageBoxWindow(string message, string caption, MessageBoxButton buttons, MessageBoxImage icon, MessageBoxResult defaultResult)
    {
        InitializeComponent();
        Title = caption;
        MessageTextBlock.Text = message;
        ConfigureIcon(icon);
        ConfigureButtons(buttons, defaultResult);
    }

    private void ConfigureIcon(MessageBoxImage icon)
    {
        // Map MessageBoxImage to SystemIcons or resources
        switch (icon)
        {
            case MessageBoxImage.None:
                IconImage.Visibility = Visibility.Collapsed;
                break;
            default:
                IconImage.Source = ImageHelpers.ToImageSource(icon);
                break;
        }
    }

    private void ConfigureButtons(MessageBoxButton buttons, MessageBoxResult defaultResult)
    {
        ButtonsPanel.Children.Clear();
        foreach ((string Text, MessageBoxResult Result) in Get(buttons))
        {
            Button button = new() 
            {
                Content = Text,
                Tag = Result,
                IsDefault = Result == defaultResult,
                IsCancel = Result == MessageBoxResult.Cancel
            };

            button.Click += Button_Click;
            ButtonsPanel.Children.Add(button);
        }

        static (string Text, MessageBoxResult Result)[] Get(MessageBoxButton buttons) => buttons switch
        {
            MessageBoxButton.OKCancel => [("_OK", MessageBoxResult.OK), ("_Cancel", MessageBoxResult.Cancel)],
            MessageBoxButton.YesNo => [("_Yes", MessageBoxResult.Yes), ("_No", MessageBoxResult.No)],
            MessageBoxButton.YesNoCancel =>
                [("_Yes", MessageBoxResult.Yes), ("_No", MessageBoxResult.No), ("_Cancel", MessageBoxResult.Cancel)],
            _ => [("_OK", MessageBoxResult.OK)],
        };
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is MessageBoxResult result)
        {
            Result = result;
            DialogResult = true;
        }
    }
}

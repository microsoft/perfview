using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace PerfView.Dialogs
{
    /// <summary>
    /// Interaction logic for NewPresetDialog.xaml
    /// </summary>
    public partial class NewPresetDialog : WindowBase
    {
        public string PresetName { get; private set; }

        public NewPresetDialog(Window parentWindow, string defaultValue, List<string> existingPresets) : base(parentWindow)
        {
            m_existingPresets = existingPresets;
            InitializeComponent();
            Title = "New Preset";
            PresetNameTextBox.Text = defaultValue;
            PresetNameTextBox.CaretIndex = defaultValue.Length;
            PresetName = defaultValue;
            PresetNameTextBox.Focus();
        }
        private void DoHyperlinkHelp(object sender, ExecutedRoutedEventArgs e)
        {
            MainWindow.DisplayUsersGuide("Preset");
        }
        private void OKClicked(object sender, RoutedEventArgs e)
        {
            // Check uniqueness of the name and ask if user wants to continue
            if (m_existingPresets.Exists(x => x == PresetNameTextBox.Text))
            {
                if (XamlMessageBox.Show(
                    $"""
                    Preset {PresetNameTextBox.Text} already exists in the list of presets.
                    Do you want to overwrite it?
                    """,
                    "Preset Name",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning) == MessageBoxResult.Cancel)
                {
                    return;
                }
            }
            DialogResult = true;
            PresetName = PresetNameTextBox.Text;
            Close();
        }

        private void DoKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }

            if (e.Key == Key.Enter)
            {
                OKClicked(null, null);
            }
        }

        private List<string> m_existingPresets;
    }
}

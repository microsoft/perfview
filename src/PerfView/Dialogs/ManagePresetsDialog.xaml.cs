using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace PerfView.Dialogs
{
    /// <summary>
    /// Interaction logic for ManagePresetsDialog.xaml
    /// </summary>
    public partial class ManagePresetsDialog : Window
    {
        public List<Preset> Presets { get; private set; }

        public ManagePresetsDialog(List<Preset> presets)
        {
            InitializeComponent();
            Title = "Manage Presets";
            Presets = presets;
            if (Presets?.Count > 0)
            {
                foreach (var preset in Presets)
                {
                    PresetListBox.Items.Add(preset.Name);
                }
                PresetListBox.SelectedItem = Presets[0].Name;
            }
        }
        private void DoHyperlinkHelp(object sender, ExecutedRoutedEventArgs e)
        {
            MainWindow.DisplayUsersGuide(e.Parameter as string);
        }
        private void OKClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
        private void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(m_currentPreset))
            {
                return;
            }

            // If preset was renamed, then check name uniqueness
            if (PresetName.Text != m_currentPreset)
            {
                if (Presets.Exists(x => x.Name == PresetName.Text))
                {
                    MessageBox.Show(
                        $"Preset '{PresetName.Text}' already exists. Choose another name.",
                        "Preset Name",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
            }

            var preset = Presets.Find(x => x.Name == m_currentPreset);
            PresetListBox.SelectedItem = PresetName.Text;
            preset.Name = PresetName.Text;
            preset.GroupPat = GroupPatsTextBox.Text;
            preset.FoldPercentage = FoldPercentTextBox.Text;
            preset.FoldPat = FoldRegExTextBox.Text;
            preset.IncPat = IncludeRegExTextBox.Text;
            preset.ExcPat = ExcludeRegExTextBox.Text;
        }
        private void DeleteClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(m_currentPreset))
            {
                return;
            }

            Presets.RemoveAll(x => x.Name == m_currentPreset);
            PresetListBox.Items.Remove(m_currentPreset);
            if (Presets.Count > 0)
            {
                PresetListBox.SelectedItem = Presets[0];
            }
            else
            {
                PresetListBox.UnselectAll();
            }
        }
        private void DoPresetSelected(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = PresetListBox.SelectedItem;
            if (selectedItem == null)
            {
                PresetName.Text = string.Empty;
                GroupPatsTextBox.Text = string.Empty;
                FoldPercentTextBox.Text = string.Empty;
                FoldRegExTextBox.Text = string.Empty;
                IncludeRegExTextBox.Text = string.Empty;
                ExcludeRegExTextBox.Text = string.Empty;
                return;
            }

            m_currentPreset = selectedItem.ToString();
            var preset = Presets.Find(x => x.Name == m_currentPreset);
            PresetName.Text = preset.Name;
            GroupPatsTextBox.Text = preset.GroupPat;
            FoldPercentTextBox.Text = preset.FoldPercentage;
            FoldRegExTextBox.Text = preset.FoldPat;
            IncludeRegExTextBox.Text = preset.IncPat;
            ExcludeRegExTextBox.Text = preset.ExcPat;
        }

        private string m_currentPreset;
    }
}

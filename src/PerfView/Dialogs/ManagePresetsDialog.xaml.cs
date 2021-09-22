using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;

namespace PerfView.Dialogs
{
    /// <summary>
    /// Interaction logic for ManagePresetsDialog.xaml
    /// </summary>
    public partial class ManagePresetsDialog : WindowBase
    {
        public List<Preset> Presets { get; private set; }

        public ManagePresetsDialog(Window parentWindow, List<Preset> presets, string basePath, StatusBar log) : base(parentWindow)
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

            m_basePath = basePath;
            m_log = log;
        }
        private void DoHyperlinkHelp(object sender, ExecutedRoutedEventArgs e)
        {
            string helpTerm = e.Parameter as string;
            if (helpTerm == "PresetList")
            {
                helpTerm = "Preset";
            }
            MainWindow.DisplayUsersGuide(helpTerm);
        }
        private void OKClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
        private void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(m_currentPreset) || PresetListBox.SelectedIndex < 0)
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
            int presetPosition = PresetListBox.Items.IndexOf(m_currentPreset);
            preset.Name = PresetName.Text;
            preset.GroupPat = GroupPatsTextBox.Text;
            preset.FoldPercentage = FoldPercentTextBox.Text;
            preset.FoldPat = FoldRegExTextBox.Text;
            preset.Comment = CommentTextBox.Text;
            PresetListBox.UnselectAll();
            PresetListBox.Items[presetPosition] = preset.Name;
            PresetListBox.SelectedItem = preset.Name;
        }
        private void DeleteClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(m_currentPreset) || PresetListBox.SelectedIndex < 0)
            {
                return;
            }

            Presets.RemoveAll(x => x.Name == m_currentPreset);
            PresetListBox.Items.Remove(m_currentPreset);
            if (Presets.Count > 0)
            {
                PresetListBox.SelectedItem = Presets[0].Name;
            }
            else
            {
                PresetListBox.UnselectAll();
            }
        }
        private void ExportPresets(object sender, RoutedEventArgs e)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.FileName = "PerfViewPresets.xml";
            saveDialog.InitialDirectory = m_basePath;
            saveDialog.Title = "File to export presets to";
            saveDialog.DefaultExt = ".xml";
            saveDialog.Filter = "PerfView presets|*.xml|All Files|*.*";
            saveDialog.AddExtension = true;
            saveDialog.OverwritePrompt = true;

            bool? result = saveDialog.ShowDialog();
            if (!(result == true))
            {
                return;
            }
            string fileName = saveDialog.FileName;
            Preset.Export(Presets, fileName);
            m_log.LogWriter.WriteLine($"[Presets exported to {fileName}.]");
        }
        private void ImportPresets(object sender, RoutedEventArgs e)
        {
            var openDialog = new Microsoft.Win32.OpenFileDialog();
            openDialog.InitialDirectory = m_basePath;
            openDialog.Title = "File to read presets from";
            openDialog.DefaultExt = "xml";
            openDialog.Filter = "PerfView presets|*.xml|All Files|*.*";
            openDialog.AddExtension = true;
            openDialog.CheckFileExists = true;

            // Show open file dialog box
            bool? result = openDialog.ShowDialog();
            if (!(result == true))
            {
                return;
            }

            string fileName = openDialog.FileName;

            Preset.Import(fileName, Presets, p => PresetListBox.Items.Add(p.Name), m_log.LogWriter);
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
                CommentTextBox.Text = string.Empty;
                return;
            }

            m_currentPreset = selectedItem.ToString();
            var preset = Presets.Find(x => x.Name == m_currentPreset);
            PresetName.Text = preset.Name;
            GroupPatsTextBox.Text = preset.GroupPat;
            FoldPercentTextBox.Text = preset.FoldPercentage;
            FoldRegExTextBox.Text = preset.FoldPat;
            CommentTextBox.Text = preset.Comment;
        }

        private string m_currentPreset;
        private string m_basePath;
        private StatusBar m_log;
    }
}

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
                    XamlMessageBox.Show(
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

            using (XmlWriter writer = XmlWriter.Create(
                fileName,
                new XmlWriterSettings() { Indent = true, NewLineOnAttributes = true }))
            {
                writer.WriteStartElement("Presets");
                foreach (Preset preset in Presets)
                {
                    writer.WriteElementString("Preset", Preset.Serialize(preset));
                }
                writer.WriteEndElement();
            }
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

            List<Preset> presetsFromFile = new List<Preset>();
            XmlReaderSettings settings = new XmlReaderSettings() { IgnoreWhitespace = true, IgnoreComments = true };
            using (XmlReader reader = XmlTextReader.Create(fileName, settings))
            {
                int entryDepth = reader.Depth;
                try
                {
                    reader.Read();
                    while (true)
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Depth > entryDepth)
                        {
                            string value = reader.ReadElementContentAsString();
                            presetsFromFile.Add(Preset.ParsePreset(value));
                            continue;
                        }

                        if (!reader.Read())
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    m_log.LogWriter.WriteLine($"[Import of presets from {fileName} has failed.]");
                    m_log.LogWriter.WriteLine("Error during reading presets file: " + ex);
                }
            }

            // Now we have current presets in Presets collection and new presets in presetsFromFile collection.
            // Existing identical presets are ignored.
            // Existing presets that differ are ignored too, but warning is written into logs.
            int imported = 0, ignored = 0;
            foreach (var preset in presetsFromFile)
            {
                var existingPreset = Presets.FirstOrDefault(x => x.Name == preset.Name);
                if (existingPreset == null)
                {
                    Presets.Add(preset);
                    PresetListBox.Items.Add(preset.Name);
                    imported++;
                    continue;
                }

                if (existingPreset.Equals(preset))
                {
                    imported++;
                    continue;
                }

                m_log.LogWriter.WriteLine($"WARN: Preset '{preset.Name}' was ignored during import because there already exist a preset with the same name.");
                ignored++;
            }
            m_log.LogWriter.WriteLine($"[Import of presets completed: {imported} imported, {ignored} ignored.]");
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

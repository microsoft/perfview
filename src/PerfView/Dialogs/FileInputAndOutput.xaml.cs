using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Input;
using Path = System.IO.Path;

namespace PerfView.Dialogs
{
    /// <summary>
    /// Interaction logic for FileInputAndOutput.xaml
    /// 
    /// It allows you to select a file (or directory) as input and also a file as output.   
    /// </summary>
    public partial class FileInputAndOutput : Window
    {

        /// <summary>
        /// Creates a class that will select a file (or directory if SelectingDirectories=true)
        /// and then call onOK, with the first argument being the input file selected and the second being the output file.
        /// After setting any properties, you can call Show() to actually open the dialog box.  
        /// </summary>
        /// <param name="onOK"></param>
        public FileInputAndOutput(Action<string, string> onOK)
        {
            m_onOK = onOK;
            InitializeComponent();
        }

        // Things you set before you call Show() to display the dialog box to the user.   They are all optional. 
        public bool SelectingDirectories { get; set; }
        public string[] InputExtentions { get; set; }
        public string OutputExtension { get; set; }
        public string HelpAnchor { get; set; }
        public string Instructions { get; set; }

        /// <summary>
        /// After calling any desired properties, call this routine to display the dialog box.  
        /// </summary>
        public new void Show()
        {
            if (CurrentDirectory == null || !Directory.Exists(CurrentDirectory))
            {
                CurrentDirectory = ".";
            }

            if (Instructions != null)
            {
                InstructionParagraph.Inlines.Clear();
                InstructionParagraph.Inlines.Add(Instructions);
            }

            if (HelpAnchor == null)
            {
                HelpHyperlink.Visibility = System.Windows.Visibility.Hidden;
            }

            OutputFileName.Text = Path.GetFullPath(CurrentDirectory) + @"\";
            InputFileTextChanged(null, null);
            InputFileName.Focus();
            ((Window)this).Show();
        }

        /// <summary>
        /// This is the directory that we currently use to populate the listbox of possible completions.  
        /// Note that has NOTHING to do with Environment.CurrentDirectory EXCEPT that Environment.CurrentDirectory
        /// is used as the initial default if it is not set before calling Show()
        /// 
        /// This is intended always a valid directory (Don't assign to it unless you know this is true).  
        /// </summary>
        public string CurrentDirectory
        {
            get { return m_CurrentDirectory; }
            set
            {
                Debug.Assert(Directory.Exists(value));
                if (value == m_CurrentDirectory)        // Optimization.  
                {
                    return;
                }

                m_CurrentDirectory = value;
                if (m_CurrentDirectory.Length == 0)
                {
                    m_CurrentDirectory = ".";
                }

                CurrentDir.Text = Path.GetFullPath(m_CurrentDirectory);
                if (string.IsNullOrEmpty(m_CurrentDirectory))
                {
                    m_candidates = new List<string>();
                }
                else
                {
                    // Get all the directories.  
                    m_candidates = new List<string>(Directory.EnumerateDirectories(m_CurrentDirectory).Select(name => Path.GetFileName(name)));
                    if (!SelectingDirectories)
                    {
                        var filteredFilePaths = Directory.EnumerateFiles(m_CurrentDirectory).Where(ExtensionFilter);
                        m_candidates.AddRange(filteredFilePaths.Select(name => Path.GetFileName(name)));
                    }

                    // Sort the list. 
                    m_candidates.Sort((x, y) => string.Compare(x, y, StringComparison.OrdinalIgnoreCase));
                    m_candidates.Add("..");
                }
            }
        }

        #region private

        private void ListBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return || e.Key == Key.Tab)
            {
                e.Handled = true;
                var selection = Files.SelectedItem as string;
                if (selection != null)
                {
                    SetInputFileName(selection);
                    if (!SelectingDirectories && e.Key == Key.Return && File.Exists(Path.Combine(CurrentDirectory, InputFileName.Text)))
                    {
                        OKClicked(null, null);
                        return;
                    }
                }
                InputFileName.Focus();
                return;
            }
        }

        private void InputFileKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CancelClicked(null, null);
                return;
            }

            if (e.Key == Key.Tab || e.Key == Key.Return)
            {
                e.Handled = true;
                var selection = Files.SelectedItem as string;
                var origFileName = InputFileName.Text;
                if (selection != null)
                {
                    SetInputFileName(selection);
                }

                if (SelectingDirectories && e.Key == Key.Return && origFileName == InputFileName.Text)
                {
                    OKClicked(null, null);
                }

                return;
            }
            if (e.Key == Key.Down)
            {
                e.Handled = true;
                var index = Files.SelectedIndex;
                if (0 <= index)
                {
                    Files.SelectedIndex = index + 1;
                }

                Files.Focus();
                return;
            }
            if (e.Key == Key.Up)
            {
                e.Handled = true;
                var index = Files.SelectedIndex;
                if (0 < index)
                {
                    Files.SelectedIndex = index - 1;
                }

                Files.Focus();
                return;
            }
        }

        private void InputFileTextChanged(object sender, RoutedEventArgs e)
        {
            // First remove any prefix that is a valid directory from 'filePath' and update CurrentDirectory.
            // The logical path Path.Combine(CurrentDirectory, filePath), is not changed under this transformation.  
            var filePath = InputFileName.Text;
            if (filePath.Length != 0)
            {
                filePath = Path.Combine(CurrentDirectory, filePath);

                // Find the largest prefix that is a valid directory
                var dir = GetValidDirectory(filePath);
                if (dir != null)
                {
                    CurrentDirectory = dir;
                    string newFilePath = filePath.Substring(dir.EndsWith(@"\") ? dir.Length : dir.Length + 1);
                    if (newFilePath != filePath)
                    {
                        InputFileName.Text = filePath = newFilePath;
                        InputFileName.CaretIndex = filePath.Length;
                    }
                }
            }

            // As this point, filePath is JUST that part of the path that is not a valid directory. 

            // Set the items in the ListBox appropriately
            string fileName = Path.GetFileName(filePath);
            var filteredList = m_candidates.Where(name => name.StartsWith(fileName, StringComparison.OrdinalIgnoreCase));
            Files.ItemsSource = filteredList;

            // Select the first item
            var first = filteredList.FirstOrDefault();
            if (first != null)
            {
                Files.SelectedItem = first;
            }

            // Update the OuputFileName as well 
            string newOutputFileName = Path.GetFileNameWithoutExtension(fileName);
            if (newOutputFileName.Length == 0 && SelectingDirectories)
            {
                var fullFilePathDir = Path.GetFullPath(CurrentDirectory);
                newOutputFileName = Path.GetFileName(fullFilePathDir);
                if (string.IsNullOrEmpty(newOutputFileName) && 2 <= fullFilePathDir.Length && fullFilePathDir.Length <= 3 && fullFilePathDir[1] == ':')
                {
                    newOutputFileName = fullFilePathDir.Substring(0, 1) + "Drive";      // if this is C:\ then use CDrive
                }
            }
            newOutputFileName += OutputExtension;
            string newOutputFilePath = ReplaceFileInPath(OutputFileName.Text, newOutputFileName);
            OutputFileName.Text = newOutputFilePath;
            var indexOfExtension = newOutputFilePath.IndexOf(OutputExtension);
            if (0 <= indexOfExtension)
            {
                OutputFileName.CaretIndex = indexOfExtension;
            }
        }

        private void FilesDoubleClick(object sender, MouseButtonEventArgs e)
        {
            string selection = Files.SelectedItem as string;
            if (selection != null)
            {
                SetInputFileName(selection);
                if (!SelectingDirectories && File.Exists(Path.Combine(CurrentDirectory, InputFileName.Text)))
                {
                    OKClicked(null, null);
                }
            }
        }

        private void OKClicked(object sender, RoutedEventArgs e)
        {
            bool success;
            string inputFileName = Path.Combine(CurrentDirectory, InputFileName.Text);
            if (SelectingDirectories)
            {
                if (inputFileName.Length == 0)
                {
                    inputFileName = ".";
                }

                success = Directory.Exists(inputFileName);
            }
            else
            {
                success = File.Exists(inputFileName);
            }

            if (success)
            {
                // Remove any trailing \
                if (inputFileName.EndsWith(@"\"))
                {
                    inputFileName = inputFileName.Substring(0, inputFileName.Length - 1);
                }

                Close();
                m_onOK(inputFileName, OutputFileName.Text);
            }
            else
            {
                SystemSounds.Beep.Play();
            }
        }

        private void CancelClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DoHyperlinkHelp(object sender, ExecutedRoutedEventArgs e)
        {
            MainWindow.DisplayUsersGuide(HelpAnchor);
        }

        /// <summary>
        /// Given a filePath, return the largest prefix that is a valid directory.  Note that
        /// if filePath ends in \ it tries that as a directory, but otherwise it will strip off
        /// the stuff after the \.  
        /// 
        /// Returns the null if there is no valid directory in the path (pretty weird)
        /// </summary>
        private static string GetValidDirectory(string filePath)
        {
            try
            {
                if (!(filePath.Length == 3 && filePath.EndsWith(@":\")))
                {
                    filePath = Path.GetDirectoryName(filePath);
                }

                while (filePath != null)
                {
                    if (Directory.Exists(filePath))
                    {
                        return filePath;
                    }

                    filePath = Path.GetDirectoryName(filePath);
                }
            }
            catch (Exception) { }
            return null;
        }

        /// <summary>
        /// Takes 'fileName' (which is a file name without a directory) and combines it with any directory currently
        /// in 'InputFileName' and updates it to have the new file name (but the same directory).
        /// </summary>
        /// <param name="fileName"></param>
        private void SetInputFileName(string fileName)
        {
            if (fileName == null)
            {
                return;
            }

            if (Directory.Exists(Path.Combine(CurrentDirectory, fileName)))
            {
                fileName += @"\";
            }

            //If we try to set the input file name when we have already set it, assume 
            // that the user wants to complete the dialog box.  Thus Tab-Tab  
            // or RETURN-RETURN is often a shorcut for (select item and close)
            if (InputFileName.Text == fileName)
            {
                OKClicked(null, null);
            }
            else
            {
                InputFileName.Text = fileName;
                InputFileName.CaretIndex = fileName.Length;
            }
        }

        /// <summary>
        /// Returns true of 'name' ends with one of the Extensions the dialog box was told to filter on.  
        /// </summary>
        private bool ExtensionFilter(string name)
        {
            if (InputExtentions == null)
            {
                return true;
            }

            foreach (var inputExtention in InputExtentions)
            {
                if (name.EndsWith(inputExtention, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Given a path 'fullPath' replace the file name part with 'fileName'.   
        /// </summary>
        private static string ReplaceFileInPath(string fullPath, string fileName)
        {
            if (0 <= fullPath.IndexOf('\\'))
            {
                string directoryName = Path.GetDirectoryName(fullPath);
                if (directoryName == null)
                {
                    // We get here if you have c:\ 
                    directoryName = fullPath;
                }
                return Path.Combine(directoryName, fileName);
            }
            else
            {
                return fileName;
            }
        }

        private Action<string, string> m_onOK;
        private List<string> m_candidates;
        private string m_CurrentDirectory;
        #endregion
    }
}

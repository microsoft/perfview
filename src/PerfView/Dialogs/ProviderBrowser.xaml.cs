using Controls;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PerfView.Dialogs
{
    /// <summary>
    /// Interaction logic for ProviderBrowser.xaml
    /// </summary>
    public partial class ProviderBrowser : WindowBase
    {
        //Level 4 is default -- after keywords
        public ProviderBrowser(Window parentWindow, Action<string, string, string> update) : base(parentWindow)
        {
            //Owner = parentWindow;
            m_keyStrings = new List<String>();
            m_selectedKeys = new List<string>();
            m_keys = new Dictionary<string, ProviderDataItem>();
            m_processNames = new List<String>();
            m_updateParent = update;
            m_level = "Verbose";
            InitializeComponent();

            ProviderNameFilter.Focus();

            LevelListBox.Items.Add("Always");
            LevelListBox.Items.Add("Critical");
            LevelListBox.Items.Add("Error");
            LevelListBox.Items.Add("Warning");
            LevelListBox.Items.Add("Informational");
            LevelListBox.Items.Add("Verbose");
            LevelListBox.SelectedItem = "Verbose";

            var processInfos = new ProcessInfos();
            m_processNames.Add("*");
            foreach (var process in processInfos.Processes)
            {
                // If the name is null, it is likely a system process, it will not have managed code, so don't bother.   
                if (process.Name == null)
                {
                    continue;
                }
                // if (process.ProcessID == myProcessId)
                //    continue;

                /*// Only show processes with GC heaps.  
                if (!allProcs && !m_procsWithHeaps.ContainsKey(process.ProcessID))
                    continue;*/
                m_processNames.Add(process.ToString());
            }

            ProcessNameListBox.ItemsSource = m_processNames;
            // Get Provider names 
            m_providerNames = new List<String>();
            foreach (Guid guid in TraceEventProviders.GetPublishedProviders())
            {
                m_providerNames.Add(TraceEventProviders.GetProviderName(guid));
                //keyStrings.Add(TraceEventProviders.GetProviderKeywords(guid).ToString());
            }

            // setup GUI controls.  
            ProviderNameListBox.ItemsSource = m_providerNames;
            KeyNameListBox.ItemsSource = m_keyStrings;
        }

        #region private
        private void DoProcessFilterTextChange(object sender, TextChangedEventArgs e)
        {
            List<String> filteredList = new List<string>();
            foreach (string processName in m_processNames)
            {
                if (0 <= processName.IndexOf(ProcessNameFilter.Text, StringComparison.OrdinalIgnoreCase))
                {
                    filteredList.Add(processName);
                }
            }
            ProcessNameListBox.ItemsSource = filteredList;
            if (filteredList.Count > 0)
            {
                ProcessNameListBox.SelectedItem = filteredList[0];
            }
        }
        private void DoProcessSelected(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = ProcessNameListBox.SelectedItem;
            if (selectedItem != null)
            {
                m_keyStrings = new List<String>();
                m_selectedKeys = new List<string>();
                m_providerNames = new List<String>();
                m_selectedProvider = null;
                m_providerNames = new List<String>();
                if (selectedItem.ToString() == "*")
                {

                    foreach (Guid guid in TraceEventProviders.GetPublishedProviders())
                    {
                        m_providerNames.Add(TraceEventProviders.GetProviderName(guid));
                    }
                }
                else
                {
                    //if (selectedItem.ToString() == "*")
                    //    TemplateProperty;
                    // else
                    m_selectedProcess = selectedItem.ToString();
                    int begin = m_selectedProcess.IndexOf("|");
                    int end = m_selectedProcess.IndexOf("| Alive", begin + 1);
                    m_selectedProcess = m_selectedProcess.Substring(begin + 8, end - begin - 8);
                    foreach (var provider in TraceEventProviders.GetRegisteredProvidersInProcess(int.Parse(m_selectedProcess)))
                    {
                        m_providerNames.Add(TraceEventProviders.GetProviderName(provider));
                    }

                    KeyNameListBox.ItemsSource = m_keyStrings;

                }
                ProviderNameListBox.ItemsSource = m_providerNames;
                updateDisplays();
            }
        }

        private void DoProviderFilterTextChange(object sender, TextChangedEventArgs e)
        {
            List<String> filteredList = new List<string>();
            foreach (string providerName in m_providerNames)
            {
                if (0 <= providerName.IndexOf(ProviderNameFilter.Text, StringComparison.OrdinalIgnoreCase))
                {
                    filteredList.Add(providerName);
                }
            }
            ProviderNameListBox.ItemsSource = filteredList;
            if (filteredList.Count > 0)
            {
                ProviderNameListBox.SelectedItem = filteredList[0];
            }
        }
        private void ProviderSelected(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = ProviderNameListBox.SelectedItem;
            if (selectedItem != null)
            {
                m_selectedProvider = selectedItem.ToString();
                m_keys = new Dictionary<string, ProviderDataItem>();
                foreach (var keyword in TraceEventProviders.GetProviderKeywords(TraceEventProviders.GetProviderGuidByName(m_selectedProvider)))
                {
                    m_keys.Add(keyword.Name.ToString(), keyword);
                }

                m_keyStrings = new List<String>();
                foreach (var key in m_keys.Keys)
                {
                    m_keyStrings.Add(m_keys[key].Name.ToString());
                }

                KeyNameListBox.ItemsSource = m_keyStrings;
                m_selectedKeys = new List<string>();
                updateDisplays();
            }
        }

        private void DoKeyFilterTextChange(object sender, TextChangedEventArgs e)
        {
            if (KeyNameFilter.Text == "*")
            {
                KeyNameListBox.ItemsSource = m_keyStrings;
            }
            else
            {
                List<String> filteredList = new List<string>();
                foreach (string key in m_keyStrings)
                {
                    if (0 <= key.IndexOf(KeyNameFilter.Text, StringComparison.OrdinalIgnoreCase))
                    {
                        filteredList.Add(key);
                    }
                }
                KeyNameListBox.ItemsSource = filteredList;
                if (filteredList.Count > 0)
                {
                    ProviderNameListBox.SelectedItem = filteredList[0];
                }
            }

        }
        private void KeySelected(object sender, SelectionChangedEventArgs e)
        {
            m_selectedKeys = new List<string>();
            foreach (var key in KeyNameListBox.SelectedItems)
            {
                m_selectedKeys.Add(key.ToString());
            }

            updateDisplays();
        }
        private void LevelSelected(object sender, SelectionChangedEventArgs e)
        {
            // Ensure at least one level is always selected
            if (LevelListBox.SelectedItem == null)
            {
                // If nothing is selected, reselect the previous level or default to "Verbose"
                string levelToSelect = !string.IsNullOrEmpty(m_level) ? m_level : "Verbose";
                LevelListBox.SelectedItem = levelToSelect;
                return;
            }
            
            m_level = LevelListBox.SelectedItem.ToString();
            updateDisplays();
        }

        private void updateDisplays()
        {
            SelectedProviderString.Text = m_selectedProvider;
            if (m_selectedKeys.Count != 0)
            {
                SelectedProviderString.Text += ":" + m_keys[m_selectedKeys[0]].Name.ToString();
                for (int i = 1; i < m_selectedKeys.Count; i++)
                {
                    SelectedProviderString.Text += "|" + m_keys[m_selectedKeys[i]].Name.ToString();
                }
            }
            else
            {
                SelectedProviderString.Text += ":*";
            }

            if (LevelListBox.SelectedItem != null)
            {
                SelectedProviderString.Text += ':' + m_level;
            }
        }
        private void DoReturnClick(object sender, RoutedEventArgs e)
        {
            string keywords = "";
            if (m_selectedKeys.Count != 0)
            {
                keywords += m_keys[m_selectedKeys[0]].Name.ToString();
            }

            for (int i = 1; i < m_selectedKeys.Count; i++)
            {
                keywords += "|" + m_keys[m_selectedKeys[i]].Name.ToString();
            }

            m_updateParent(m_selectedProvider, keywords, m_level);
            Close();
        }

        private void DoViewManifestClick(object sender, RoutedEventArgs e)
        {
            StatusBar log = GuiApp.MainWindow.StatusBar;

            string selectedProvider = ProviderNameListBox.SelectedItem as string;
            if (selectedProvider == null)
            {
                log.LogError("No provider selected");
                return;
            }
            log.Log("[Looking up manifest for " + selectedProvider + " ]");
            string manifestString = RegisteredTraceEventParser.GetManifestForRegisteredProvider(selectedProvider);

            var textEditorWindow = new TextEditorWindow(this);
            textEditorWindow.Width = 1200;
            textEditorWindow.Height = 800;
            textEditorWindow.TextEditor.IsReadOnly = true;
            textEditorWindow.TextEditor.AppendText(manifestString);
            textEditorWindow.Show();
        }

        private void DoHyperlinkHelp(object sender, ExecutedRoutedEventArgs e)
        {
            string command = e.Parameter as string;
            MainWindow.DisplayUsersGuide("ProviderBrowser" + command);
        }

        // Fields 
        private List<String> m_processNames;
        private string m_selectedProcess;
        private List<String> m_providerNames;
        private string m_selectedProvider;
        private List<String> m_keyStrings;
        private Dictionary<String, ProviderDataItem> m_keys;
        private List<String> m_selectedKeys;
        private string m_level;
        private Action<string, string, string> m_updateParent;
        #endregion
    }
}

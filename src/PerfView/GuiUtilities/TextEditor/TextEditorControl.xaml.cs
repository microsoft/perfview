using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace Controls
{
    /// <summary>
    /// A TextEditorControl is a richTextBox with search, open, and save (basically notepad)
    /// </summary>
    public partial class TextEditorControl : UserControl
    {
        public TextEditorControl()
        {
            InitializeComponent();
        }
        /// <summary>
        /// Is the user allowed to modify the text. 
        /// </summary>
        public bool IsReadOnly { get { return Body.IsReadOnly; } set { Body.IsReadOnly = value; } }
        /// <summary>
        /// The document representing the text in the Editor.   Most manipuation operations 
        /// are through this object (via TextPointers and TextRanges).  
        /// </summary>
        public FlowDocument Document { get { return Body.Document; } }
        /// <summary>
        /// The body of the editor as text.  Generally this is not the best way of retriving the
        /// data.   You should be using operators TextRange or TextPointer fetched from Document.  
        /// This is more of a sample of how to use TextRange and TextPointer.  
        /// </summary>
        public string Text
        {
            get { return new TextRange(Body.Document.ContentStart, Body.Document.ContentEnd).Text; }
            set { new TextRange(Body.Document.ContentStart, Body.Document.ContentEnd).Text = value; }
        }
        /// <summary>
        /// Appends text to the editor's text buffer. 
        /// </summary>
        public void AppendText(string textData)
        {
            Body.AppendText(textData);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        public void OpenText(string fileName)
        {
            try
            {
                using (var stream = File.OpenRead(fileName))
                {
                    new TextRange(Body.Document.ContentStart, Body.Document.ContentEnd).Load(stream, DataFormats.Text);
                }

                m_fileName = fileName;
            }
            catch (Exception)
            {
                SystemSounds.Beep.Play();
            }
        }
        public void SaveText(string fileName)
        {
            try
            {
                using (var stream = File.Create(fileName))
                {
                    new TextRange(Body.Document.ContentStart, Body.Document.ContentEnd).Save(stream, DataFormats.Text);
                }

                m_fileName = fileName;
            }
            catch (Exception)
            {
                SystemSounds.Beep.Play();
            }
        }

        /// <summary>
        /// Selects the line at 'lineNum' (the first line is 1).   Will select the last line
        /// if lineNum is greater than the number of lines.   It is actually counting paragraphs
        /// so lines that are not text don't count (effectively it is counting line feeds. 
        /// </summary>
        public void GotoLine(int lineNum)
        {
            // Line numbers start at 1;
            if (lineNum > 0)
            {
                --lineNum;
            }

            IEnumerable<Block> blocks = Body.Document.Blocks;
            if (blocks == null)
            {
                return;
            }

            Block blockForLine = null;
            foreach (Block block in blocks)
            {
                blockForLine = block;
                if (lineNum <= 0)
                {
                    break;
                }

                if (block is Paragraph)
                {
                    --lineNum;
                }
            }
            if (blockForLine != null)
            {
                Body.Selection.Select(blockForLine.ContentStart, blockForLine.ContentEnd);
                blockForLine.BringIntoView();
            }
        }

        /// <summary>
        /// Finds 'pattern' in the text starting at the current selection point and wrapping around
        /// until all the text is searched.  It will set the selected text to the found text and
        /// return a TextPointer to the beginning of the selected text (or null if it fails. 
        /// </summary>
        /// <param name="pattern">The .NET regular expression to match.</param>
        /// <returns>Returns a text pointer to the found text or null if not matched. </returns>
        public TextPointer Find(string pattern)
        {
            try
            {
                var pat = new Regex(pattern, RegexOptions.IgnoreCase);

                var stopPos = Body.Selection.Start;
                var pos = Body.Selection.End;
                Block startBlock = pos.Paragraph;
                if (startBlock == null)
                {
                    return null;
                }

                Block block = startBlock;
                var endBlock = block.ContentEnd;
                var wrapped = false;
                for (; ; )
                {
                    var text = new TextRange(pos, endBlock).Text;
                    var match = pat.Match(text);
                    if (match.Success)
                    {
                        var start = GetTextPointerFromTextOffset(pos, match.Index);
                        var end = GetTextPointerFromTextOffset(start, match.Length);
                        Body.Selection.Select(start, end);
                        start.Paragraph.BringIntoView();
#if DEBUG
                        var selectedText = Body.Selection.Text;
                        match = pat.Match(selectedText);
                        Debug.Assert(match.Success && match.Index == 0 && match.Length == selectedText.Length);
#endif
                        Body.Focus();
                        return start;
                    }

                    // We have searched the entire file and found nothing.  
                    if (block == startBlock && wrapped)
                    {
                        SystemSounds.Beep.Play();
                        return null;
                    }

                    // Advance to the next line, wrapping to the first block 
                    block = block.NextBlock;
                    if (block == null)
                    {
                        wrapped = true;
                        block = Body.Document.Blocks.FirstBlock;
                    }
                    // Load up the beginning and end of that block.  
                    pos = block.ContentStart;
                    if (block != startBlock)
                    {
                        endBlock = block.ContentEnd;
                    }
                    else
                    {
                        endBlock = stopPos;     // The last block we stop where we began the search.  
                    }
                }
            }
            catch (Exception)
            {
                SystemSounds.Beep.Play();       // Indicate an error. 
                return null;
            }
        }

        #region private
        // User defined commands
        public static RoutedUICommand FindNextCommand = new RoutedUICommand("Find Next", "FindNext", typeof(TextEditorControl),
            new InputGestureCollection() { new KeyGesture(Key.F3) });
        public static RoutedUICommand DeleteLineCommand = new RoutedUICommand("Delete Line", "DeleteLine", typeof(TextEditorControl),
            new InputGestureCollection() { new KeyGesture(Key.L, ModifierKeys.Alt) });
        public static RoutedUICommand ClearCommand = new RoutedUICommand("Clear", "Clear", typeof(TextEditorControl),
            new InputGestureCollection() { new KeyGesture(Key.C, ModifierKeys.Alt) });

        // Command Callbacks. 
        private void DoFindNext(object sender, ExecutedRoutedEventArgs e)
        {
            Find(FindTextBox.Text);
        }
        private void DoFind(object sender, ExecutedRoutedEventArgs e)
        {
            if (!Body.Selection.IsEmpty)
            {
                FindTextBox.Text = Body.Selection.Text;
            }

            if (FindTextBox.Text.Length == 0)
            {
                FindTextBox.Text = "Enter Search Text";
            }

            FindTextBox.Focus();
        }
        private void DoDeleteLine(object sender, ExecutedRoutedEventArgs e)
        {
            EditingCommands.MoveToLineStart.Execute(null, Body);
            EditingCommands.SelectDownByLine.Execute(null, Body);
            ApplicationCommands.Cut.Execute(null, Body);
        }
        private void DoClear(object sender, ExecutedRoutedEventArgs e)
        {
            Body.SelectAll();
            Body.Selection.Text = "";
        }
        private void DoClose(object sender, ExecutedRoutedEventArgs e)
        {
            Window asWindow = Parent as Window;
            if (asWindow != null)
            {
                asWindow.Close();
            }
        }
        private void DoSaveAs(object sender, ExecutedRoutedEventArgs e)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.Title = "File Save";
            saveDialog.DefaultExt = ".txt";                  // Default file extension
            saveDialog.Filter = "Text File|*.txt;*.log|All Files|*.*";     // Filter files by extension
            saveDialog.AddExtension = true;
            saveDialog.OverwritePrompt = true;
            if (m_fileName != null)
            {
                saveDialog.FileName = Path.GetFileName(m_fileName);
            }

            // Show open file dialog box
            Nullable<bool> result = saveDialog.ShowDialog();

            // Process open file dialog box results
            if (result == true && !string.IsNullOrEmpty(saveDialog.FileName))
            {
                m_fileName = saveDialog.FileName;
                Window window = Parent as Window;
                if (window != null)
                {
                    window.Title = "Editing: " + m_fileName;
                }

                SaveText(m_fileName);
            }
            else
            {
                SystemSounds.Beep.Play();
            }
        }
        private void DoSave(object sender, ExecutedRoutedEventArgs e)
        {
            if (m_fileName == null)
            {
                DoSaveAs(sender, e);
            }
            else
            {
                SaveText(m_fileName);
            }
        }
        private void DoOpen(object sender, ExecutedRoutedEventArgs e)
        {
            var openDialog = new Microsoft.Win32.OpenFileDialog();
            openDialog.Title = "File Save";
            openDialog.DefaultExt = ".txt";                  // Default file extension
            openDialog.Filter = "Text File|*.txt;*.log";     // Filter files by extension
            openDialog.AddExtension = true;
            openDialog.CheckFileExists = true;

            // Show open file dialog box
            Nullable<bool> result = openDialog.ShowDialog();

            // Process open file dialog box results
            if (result == true && !string.IsNullOrEmpty(openDialog.FileName))
            {
                m_fileName = openDialog.FileName;
                Window window = Parent as Window;
                if (window != null)
                {
                    window.Title = "Editing: " + m_fileName;
                }

                OpenText(m_fileName);
            }
            else
            {
                SystemSounds.Beep.Play();
            }
        }

        // GUI callbacks
        private void FindTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                Find(FindTextBox.Text);
            }
        }
        private void IncreaseFont_Click(object sender, RoutedEventArgs e)
        {
            Body.FontSize = Math.Min(Body.FontSize + 2, 36);
        }
        private void DecreaseFont_Click(object sender, RoutedEventArgs e)
        {
            Body.FontSize = Math.Max(Body.FontSize - 2, 6);
        }
        private TextPointer GetTextPointerFromTextOffset(TextPointer start, int textCharacterOffset)
        {
            while (textCharacterOffset > 0)
            {
                int charsInRun = start.GetTextRunLength(LogicalDirection.Forward);
                string run = start.GetTextInRun(LogicalDirection.Forward);
                if (textCharacterOffset <= charsInRun)
                {
                    return start.GetPositionAtOffset(textCharacterOffset);
                }

                textCharacterOffset -= charsInRun;
                start = start.GetNextContextPosition(LogicalDirection.Forward);
            }
            return start;
        }

        private string m_fileName;
        #endregion
    }

    /// <summary>
    /// TextEditorWriter creates a TextWriter that sends it TextEditor.  
    /// </summary>
    internal class TextEditorWriter : TextWriter
    {
        public TextEditorWriter(TextEditorControl textEditorControl)
        {
            m_textEditorControl = textEditorControl;
            m_sb = new StringBuilder();
            m_timer = new DispatcherTimer();
            m_timer.Tick += delegate { Flush(); };
            m_timer.Interval = new TimeSpan(100000 * 300);     // 300 msec 
            m_textEditorControl.IsVisibleChanged += delegate (object sender, DependencyPropertyChangedEventArgs e) { Flush(); };
        }
        public override void Write(char value)
        {
            Write(new String(value, 1));
        }
        public override void Write(char[] buffer, int index, int count)
        {
            Write(new String(buffer, index, count));
        }
        public override void Flush()
        {
            m_timer.Stop();

            if (m_textEditorControl.IsVisible && m_sb.Length > 0)
            {
                m_textEditorControl.Dispatcher.BeginInvoke((Action)delegate ()
                {
                    lock (this)
                    {
                        // Flushing is expensive, do it no more frequently than once every 200 msec
                        if ((DateTime.UtcNow - m_LastTimeFlushed).TotalMilliseconds > 200)
                        {
                            // The Text control gets unresponsive if it is too big.  Scroll the data, but also put it in a file
                            const int maxLengthInViewer = 50000;        // TODO make this a parameter
                            string logData = m_sb.ToString();
                            m_sb.Clear();
                            var newTotalLen = logData.Length + m_charsWritten;
                            if (newTotalLen < maxLengthInViewer)
                            {
                                m_textEditorControl.AppendText(logData);
                            }
                            else
                            {
                                if (logData.Length < maxLengthInViewer)
                                {
                                    logData = m_textEditorControl.Text + logData;
                                }

                                var newLogData = @"***** See " + PerfView.App.LogFileName + " for complete log. ******\r\n";
                                var dataLen = Math.Min(maxLengthInViewer, logData.Length);
                                newLogData += logData.Substring(logData.Length - dataLen, dataLen);
                                m_textEditorControl.Text = newLogData;
                            }
                            m_textEditorControl.Body.ScrollToEnd();
                            m_charsWritten = newTotalLen;
                            m_LastTimeFlushed = DateTime.UtcNow;
                        }
                    }
                });
            }
            base.Flush();
        }
        public override void Write(string value)
        {
            if (value.Length == 0)
            {
                return;
            }

            lock (this)
            {
                if (!m_timer.IsEnabled)
                {
                    m_timer.Start();
                }

                m_sb.Append(value);

                if (value.EndsWith("\r\n"))
                {
                    value = value.Substring(0, value.Length - 2);
                }

                PerfViewLogger.Log.PerfViewLog(value);
            }
        }
        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }
        public string GetText()
        {
            lock (this)
            {
                return m_sb.ToString() + m_textEditorControl.Text;
            }
        }

        public override string ToString()
        {
            lock (this)
            {
                return m_textEditorControl.Text + " PENDING " + m_sb.ToString();
            }
        }
        #region private
        private const int BuffSize = 10240;
        protected TextEditorControl m_textEditorControl;
        private StringBuilder m_sb;
        private DispatcherTimer m_timer;
        private DateTime m_LastTimeFlushed;
        private int m_charsWritten;
        #endregion
    }
}

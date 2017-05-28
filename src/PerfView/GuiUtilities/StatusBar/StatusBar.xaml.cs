using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Threading;
using System.Media;
using System.Diagnostics;
using System.Windows.Media;
using Controls;
using System.IO;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Windows.Input;
using Utilities;
using Microsoft.Diagnostics.Utilities;
using System.Threading.Tasks;

namespace PerfView
{
    /// <summary>
    /// Interaction logic for StatusBar.xaml
    /// </summary>
    public partial class StatusBar : UserControl, INotifyPropertyChanged
    {
        public StatusBar()
        {
            m_blinkColor = new SolidColorBrush(Color.FromRgb(0x42, 0x69, 0xEB));
            m_timer = new DispatcherTimer();
            m_workMessage = "";
            m_timer.Interval = new TimeSpan(0, 0, 0, 0, 600);
            m_timer.Tick += OnTick;

            InitializeComponent();

            m_StatusMessage.PreviewMouseDoubleClick += delegate(object sender, MouseButtonEventArgs e)
            {
                e.Handled = StatusBar.ExpandSelectionByANumber(m_StatusMessage);
                return;
            };
        }
        /// <summary>
        /// Report messages are short messages that are not persisted in the history, these are meant for
        /// messages that are not in direct response to a user command (and thus might be 'noise')
        /// </summary>
        public string Status { get { return m_StatusMessage.Text; } set { m_StatusMessage.Text = value; m_loggedStatus = true; LoggedError = false; } }
        public TimeSpan Duration { get { return DateTime.Now - m_startTime; } }
        /// <summary>
        /// Logging a message on the other hand, is persisted, and can be viewed later.  These should be
        /// used for any messages that are in direct response to an obvious user command (Effectively 
        /// logging what the user did.  
        /// 
        /// This can called by the worker thread, and Log() will insure that it posts the message on the GUI thread
        /// </summary>
        public void Log(string message)
        {
            LogWriter.WriteLine(message);
            System.Threading.Thread.Sleep(0);       // Allow Abort
        }
        /// <summary>
        /// Logs an error message (logs and then beeps).  
        /// </summary>
        /// <param name="errorMessage"></param>
        public void LogError(string errorMessage)
        {
            Log(errorMessage);
            SystemSounds.Beep.Play();
            if (errorMessage.IndexOf('\n') < 0)     // Is it a one line error message?
            {
                if (Dispatcher.CheckAccess())
                    Status = errorMessage;
                else
                    Dispatcher.BeginInvoke((Action)delegate()
                    {
                        Status = errorMessage;
                    });
            }
            LoggedError = true;
        }
        /// <summary>
        /// Play a beep sound. 
        /// </summary>
        public void Beep()
        {
            SystemSounds.Beep.Play();
        }
        /// <summary>
        /// Have we just logged an error (last message in status bar is an error message)
        /// </summary>
        public bool LoggedError { get; private set; }
        /// <summary>
        /// returns a TextWriter for the status log.  
        /// </summary>
        public TextWriter LogWriter
        {
            get
            {
                if (m_logWriter == null)
                {
                    AttachWriterToLogStream(null);
                    m_logWriter = new StatusTextWriter(this, s_logWriter);
                }
                return m_logWriter;
            }
        }

        /// <summary>
        /// This causes any text written to the status log to also be written to 'writer' as well.  
        /// </summary>
        public static void AttachWriterToLogStream(TextWriter writer)
        {
            if (s_logWriter == null)
                s_logWriter = new TextEditorWriter(LogWindow.TextEditor);

            if (writer != null)
            {
                if (!(s_logWriter is TextEditorWriter))
                    throw new InvalidOperationException("Only one writer can be attached to the log stream simultaneously");
                s_logWriter = new TeeTextWriter(writer, s_logWriter);
            }
        }

        /// <summary>
        /// This fetches the window which has the log text in it 
        /// </summary>
        public static TextEditorWindow LogWindow
        {
            get
            {
                if (s_log == null)
                {
                    s_log = new TextEditorWindow();
                    s_log.HideOnClose = true;
                    s_log.Title = "Status Log";
                }
                return s_log;
            }
        }
        /// <summary>
        /// The status bar has a log associated with it.  This opens the window (if it is not already open)
        /// </summary>
        public void OpenLog()
        {
            LogWriter.Flush();
            LogWindow.Show();
            LogWindow.TextEditor.Body.ScrollToEnd();
            LogWindow.Focus();
        }

        /// <summary>
        /// This starts long running work.  It is called on the GUI thread.
        /// Only one piece of work can be running at a time (this is simply to keep the
        /// model the user sees simple).  
        /// 
        /// If finally_ is present, it will be run at EndWork time (But before the 
        /// 'response' delegate passed to EndWork).   Logically it is a finally clause associated
        /// with the work (but not the 'response')  Unlike 'response' this action
        /// will occur under ALL conditions, including cancellation.   it is used for other
        /// GUID indications that work is in progress.  
        /// 
        /// If 'finally_' is present it will be executed on the GUI thread.  
        /// </summary>
        public void StartWork(string message, Action work, Action finally_ = null)
        {
            // We only call this from the GUI thread
            if (Dispatcher.Thread != Thread.CurrentThread)
                throw new InvalidOperationException("Work can only be started from the UI thread.");

            // Because we are only called on the GUI thread, there is no race.
            if (m_work != null)
            {
                // PerfViewLogger.Log.DebugMessage("Must cancel " + m_workMessage + " before starting " + message);
                LogError("Must first cancel: " + m_workMessage + " before starting " + message);
                return;
            }
            // PerfViewLogger.Log.DebugMessage("Starting Working " + message);
            m_abortStarted = false;
            m_abortDidInterrupt = false;
            m_endWorkStarted = false;
            m_endWorkCompleted = false;
            m_worker = null;
            m_finally = finally_;
            if (m_parentWindow == null)
                m_parentWindow = Helpers.AncestorOfType<Window>(this);
            if (m_parentWindow != null)
            {
                m_origCursor = m_parentWindow.Cursor;
                m_parentWindow.Cursor = System.Windows.Input.Cursors.Wait;
            }

            // Update GUI state
            m_workTimeSec = 0;
            m_startTime = DateTime.Now;
            m_timer.IsEnabled = true;
            m_CancelButton.IsEnabled = true;
            m_workMessage = message;
            m_ProgressText.Text = "Working";
            Background = m_blinkColor;
            var completeMessage = "Started: " + message;
            Status = completeMessage;
            LogWriter.WriteLine(completeMessage);
            m_loggedStatus = false;

            // This part may take a little bit of time, so we pass it off to another
            // thread (to keep the UI responsive, and then when it is done call
            // back (this.BeginInvoke), to finish it off.
            var currentCulture = CultureInfo.CurrentCulture;
            var currentUICulture = CultureInfo.CurrentUICulture;
            m_work = Task.Run(() =>
            {
                var oldCulture = Tuple.Create(CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
                try
                {
                    Thread.CurrentThread.CurrentCulture = currentCulture;
                    Thread.CurrentThread.CurrentUICulture = currentUICulture;

                    try
                    {
                        try
                        {
                            m_worker = Thread.CurrentThread;    // At this point we can be aborted.  
                            // If abort was called before m_worker was initialized we need to kill this thread ourselves.  
                            if (m_abortStarted)
                                throw new ThreadInterruptedException();
                            work();
                            Debug.Assert(m_endWorkStarted, "User did not call EndWork before returning from work body.");
                        }
                        catch (Exception ex)
                        {
                            EndWork(delegate()
                            {
                                if (!(ex is ThreadInterruptedException))
                                {
                                    bool userLevel;
                                    var errorMessage = ExceptionMessage.GetUserMessage(ex, out userLevel);
                                    if (userLevel)
                                        LogError(errorMessage);
                                    else
                                    {
                                        Log(errorMessage);
                                        LogError("An exceptional condition occurred, see log for details.");
                                    }
                                }
                            });
                        }
                        Debug.Assert(m_worker == null || m_abortStarted);     // EndWork should have been called and nulled this out.   

                        // If we started an abort, then a thread-interrupt might happen at any time until the abort is completed.  
                        // Thus we should wait around until the abort completes.  
                        if (m_abortStarted)
                            while (!m_abortDidInterrupt)
                                Thread.Sleep(1);
                    }
                    catch (ThreadInterruptedException) { }      // we 'expect' ThreadInterruptedException so don't let them leak out. 

                    // Cancellation completed, means that the thread is dead.   We don't allow another work item on this StatusBar 
                    // The current thread is dead.

                    Debug.Assert(m_endWorkStarted);
                    if (m_abortStarted)
                        Log("Cancellation Complete on thread " + Thread.CurrentThread.ManagedThreadId + " : (Elapsed Time: " + Duration.TotalSeconds.ToString("f3") + " sec)");
                }
                finally
                {
                    Thread.CurrentThread.CurrentCulture = oldCulture.Item1;
                    Thread.CurrentThread.CurrentUICulture = oldCulture.Item2;
                }
            });

            SignalPropertyChange(nameof(IsWorking));
            SignalPropertyChange(nameof(IsNotWorking));
        }
        /// <summary>
        /// This is used by the thread off the GUI thread to post back a response.  It also informs
        /// the GUI that works is done.   This should only be called from the worker thread (use
        /// Abort() to force an abort).  
        /// </summary>
        public void EndWork(Action response)
        {
            // EndWork should only be called from the worker.  
            Debug.Assert(m_worker == null || Thread.CurrentThread == m_worker);
            Debug.Assert(m_work != null, "Called EndWork before work was started");
            m_endWorkStarted = true;
            Dispatcher.BeginInvoke((Action)delegate()
            {
                // m_endWorkDone insures that we call EndWork at most once. 
                if (!m_endWorkCompleted)
                {
                    m_endWorkCompleted = true;
                    m_timer.IsEnabled = false;
                    m_CancelButton.IsEnabled = false;
                    m_ProgressText.Text = "Ready";
                    Background = Brushes.Transparent;
                    var message = (m_abortStarted ? "Aborted: " : "Completed: ") + m_workMessage +
                        "   (Elapsed Time: " + Duration.TotalSeconds.ToString("f3") + " sec)";
                    LogWriter.WriteLine(message);
                    // We don't want important status being lost
                    // Only update the status if there were no other updates 
                    if (!m_loggedStatus)
                        Status = message;

                    // PerfViewLogger.Log.DebugMessage("Stopping working " + m_workMessage);
                    m_workMessage = "";
                    if (m_parentWindow != null)
                        m_parentWindow.Cursor = m_origCursor;

                    m_work = null;
                    SignalPropertyChange(nameof(IsWorking));
                    SignalPropertyChange(nameof(IsNotWorking));
                    response?.Invoke();
                    m_finally?.Invoke();
                }
            });
            m_worker = null;        // After we call end-work, you don't want to be able to abort the thread
        }
        /// <summary>
        /// Abort the piece of work that is in flight (if any).  It returns promptly and the abort may not
        /// be complete by the time it returns.   Abort will be complete when IsWorking==false.  
        /// 
        /// If silent is true then no log message is sent.  Useful for 'expected' aborts which would otherwise clutter the log.  
        /// </summary>
        public void AbortWork(bool silent = false)
        {
            if (!silent)
                Log("Abort Requested.");
            if (m_work == null)
            {
                if (!silent)
                    Log("No work in progress.  Abort skipped.");
                return;
            }

            // We only call this from the GUI thread, We can probably relax this, but reasoning about races is easier.  
            Debug.Assert(Dispatcher.Thread == Thread.CurrentThread);

            if (!m_abortStarted)
            {
                m_abortStarted = true;           // Once m_aborting is set, thread is guarnteed to stick around until m_abortCompleted is true.  
                Thread worker = m_worker;
                if (worker != null)
                {
                    if (!silent)
                        Log("Cancelation Started on thread " + worker.ManagedThreadId + " : " + m_workMessage + " (Elapsed Time: " + Duration.TotalSeconds.ToString("f3") + " sec)");
                    worker.Interrupt();           // kill any work in process;
                }
                m_abortDidInterrupt = true;       // Indicate that we will never send an interrupt again

                // When the thread finally dies, indicate this by calling EndWork.  
                ThreadPool.QueueUserWorkItem(delegate
                {
                    while (m_worker != null)          // Wait for abort to complete.  
                        Thread.Sleep(10);

                    // Because we interrupted the worker, We can't rely on the worker to do EndWork, so we do it.  
                    // EndWork will insure that if we try to do the work twice, only the first will succeed.  
                    EndWork(null);
                });
            }
            else
            {
                Log("Still waiting on cancellation...");
                // TODO FIX NOW remove m_worker.Interrupt();           // kill any work in process;
            }

        }

        /// <summary>
        /// In the case where the work has no completion action other than updating status, this routine
        /// can do all the work (startWord, do work endWork)
        /// </summary>
        public void DoWork(string message, Action work)
        {
            StartWork(message, delegate()
            {
                work();
                EndWork(delegate()
                {
                    Status = "Complete: " + message;
                });
            });
        }
        /// <summary>
        /// returns true if we have started work and have not completed it. 
        /// </summary>
        public bool IsWorking { get { return m_work != null; } }
        /// <summary>
        /// returns true if we there is no pending work. 
        /// </summary>
        public bool IsNotWorking { get { return m_work == null; } }

        /// <summary>
        /// Waits for any outstanding work to complete.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task WaitForWorkCompleteAsync()
        {
            while (true)
            {
                if (!IsWorking)
                    return;

                var work = m_work;
                if (work != null)
                {
                    await work.ConfigureAwait(false);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(1)).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Give a textBox, expand the selection to select the whole number
        /// returns true if successful.  Meant to be used for TextBox doubleClick events.  
        /// TODO this really does not belong here. 
        /// </summary>
        public static bool ExpandSelectionByANumber(TextBox textBox)
        {
            var text = textBox.Text;
            if (text.Length == 0)
                return false;

            var start = textBox.SelectionStart;
            if (start >= text.Length)
                start = text.Length - 1;
            var end = start;
            while (start > 0)
            {
                var c = text[start - 1];
                if (!(Char.IsDigit(c) || c == ',' || c == '.'))
                    break;
                --start;
            }
            // We accept negative numbers too.  
            if (start > 0 && text[start - 1] == '-')
                --start;

            while (end < text.Length)
            {
                var c = text[end];
                if (!(Char.IsDigit(c) || c == ',' || c == '.'))
                    break;
                end++;
            }

            if (end > start)
            {
                textBox.SelectionStart = start;
                textBox.SelectionLength = end - start;
                return true;
            }
            return false;
        }

        #region private
        private void OnTick(object sender, EventArgs e)
        {
            m_workTimeSec++;
            // Make the background blink
            Background = ((m_workTimeSec & 1) != 0) ? Brushes.Transparent : m_blinkColor;

            m_ProgressText.Text = "Working " + m_workTimeSec.ToString();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            AbortWork();
        }
        private void Log_Click(object sender, RoutedEventArgs e)
        {
            OpenLog();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void SignalPropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // These are GUI state, and only the GUI thread cares about it.    
        static TextEditorWindow s_log;
        static TextWriter s_logWriter;           // This is  IS shared.  
        StatusTextWriter m_logWriter;            // This is a StatusTextWriter and is NOT shared.

        DispatcherTimer m_timer;
        int m_workTimeSec;
        DateTime m_startTime;
        internal string m_workMessage;
        Brush m_blinkColor;
        Window m_parentWindow;
        System.Windows.Input.Cursor m_origCursor;
        Task m_work;
        Action m_finally;                   // work that is done wehther the command succeeds or not 
        bool m_loggedStatus;                // Did we send anything to the status bar?

        // State touched by the worker thread. 
        volatile bool m_abortStarted;        // The Abort() API was called.   Set in Gui, read in worker
        volatile bool m_abortDidInterrupt;      // The Abort() API is finished.  Set in Gui, read in worker
        volatile Thread m_worker;            // As long as this is set, we could be aborted aynchronously.  
        bool m_endWorkStarted;               // always set from worker thread, just for asserts. 
        bool m_endWorkCompleted;             // Did we do all the work in endWork, we use this to only do EndWork no more than once.  
        #endregion
    }

    internal class StatusTextWriter : TextWriter
    {
        public StatusTextWriter(StatusBar statusBar, TextWriter stream)
        {
            m_statusBar = statusBar;
            m_stream = stream;
        }
        public TextWriter BaseStream { get { return m_stream; } }

        public override void WriteLine(string value)
        {
            UpdateStatus(value);
            m_stream.WriteLine(value);
        }
        public override void Write(string value)
        {
            UpdateStatus(value);
            m_stream.Write(value);
        }

        public override System.Text.Encoding Encoding { get { return m_stream.Encoding; } }
        public override void Write(char value) { m_stream.Write(value); }
        public override void Flush() { m_stream.Flush(); }
        protected override void Dispose(bool disposing) { if (disposing) { m_stream.Dispose(); } }

        private void UpdateStatus(string value)
        {
            if (value == null)
                return;

            Match m = Regex.Match(value, @"^\s*\[(.*)\]\s*$");
            if (m.Success)
            {
                m_statusBar.Dispatcher.BeginInvoke((Action)delegate()
                {
                    if (m_statusBar.Visibility != Visibility.Visible)
                        m_statusBar.Visibility = Visibility.Visible;
                    m_statusBar.Status = m.Groups[1].Value;
                });
            }
        }

        StatusBar m_statusBar;
        TextWriter m_stream;
    }

}


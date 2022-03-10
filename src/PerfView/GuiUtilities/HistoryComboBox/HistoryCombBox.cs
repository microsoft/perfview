using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Controls
{
    /// <summary>
    /// A trivial variation on a ComboBox that always remembers the last several entries entered into it.
    /// It is intended to be a drop-in replacement for TextBox.  
    /// </summary>
    public partial class HistoryComboBox : ComboBox
    {
        public HistoryComboBox()
        {
            IsEditable = true;
            HistoryLength = 10;
            KeyDown += DoKeyDown;
            GotFocus += DoGotFocus;
            LostFocus += DoLostFocus;
            SelectionChanged += DoComboSelectionChanged;
            DropDownClosed += DoDropDownClosed;
            Items.Add("");
            // TODO REMOVE var dpd = DependencyPropertyDescriptor.FromProperty(TextProperty, typeof(ComboBox));
            // dpd.AddValueChanged(this, DoTextChanged);
        }
        public int HistoryLength { get; set; }

        public void SetHistory(IEnumerable values)
        {
            Items.Clear();
            int count = 0;
            foreach (var value in values)
            {
                count++;
                if (count >= HistoryLength)
                {
                    break;
                }

                Items.Add(value);
            }
        }
        public void RemoveFromHistory(string value)
        {
            var text = Text;
            for (int i = 0; i < Items.Count; i++)
            {
                if ((string)Items[i] == value)
                {
                    Items.RemoveAt(i);
                    Text = text;
                    break;
                }
            }
        }
        public bool AddToHistory(string value)
        {
            if (Items.Count > 0 && ((string)Items[0]) == value)       // Common special case that does nothing.  
            {
                return false;
            }

            RemoveFromHistory(value);
            Items.Insert(0, value);
            Text = value;

            // Keep the number of entries under control
            while (Items.Count > HistoryLength)
            {
                Items.RemoveAt(HistoryLength);
            }

            return true;
        }

        /// <summary>
        /// This event fires when focus is lost or and Enter is typed in the box.  
        /// </summary>
        public event RoutedEventHandler TextEntered;
        /// <summary>
        /// This fires only when the enter character is typed or a combo box item is selected.  
        /// </summary>
        public event RoutedEventHandler Enter;
        public void CopyFrom(HistoryComboBox other)
        {
            foreach (var item in other.Items)
            {
                Items.Add(item);
            }
        }

        #region private

        private void DoComboSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            m_selectedItem = null;
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                m_selectedItem = e.AddedItems[0].ToString();
            }
        }

        private void DoGotFocus(object sender, RoutedEventArgs e)
        {
            m_hasFocus = true;
            m_origBackground = Background;
            Background = new SolidColorBrush(Color.FromRgb(0xDC, 0xE5, 0xEB));
        }
        private void DoLostFocus(object sender, RoutedEventArgs e)
        {
            bool prevFocus = m_hasFocus;
            m_hasFocus = false;
            if (m_origBackground != null)
            {
                Background = m_origBackground;
            }

            if (prevFocus)
            {
                TextEntered?.Invoke(sender, e);
                ValueUpdate();
            }
        }
        private void DoKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                FireEnter(sender, e);
            }
        }
        private void DoDropDownClosed(object sender, EventArgs e)
        {
            if (m_selectedItem != null)
            {
                Text = m_selectedItem;
            }

            FireEnter(sender, e as RoutedEventArgs);
        }

        /// <summary>
        /// Force an callback as if you hit the Enter Key.  
        /// </summary>
        private void FireEnter(object sender, RoutedEventArgs e)
        {
            var text = GetTextBox().Text;

            if (text.Length > 0)
            {
                AddToHistory(Text);
            }

            // Logically we have lost focus.  (Any way of giving it up for real?)
            m_hasFocus = false;

            var enter = Enter;
            if (enter != null)
            {
                Enter(sender, e);
            }

            TextEntered?.Invoke(sender, e);

            ValueUpdate();
        }
        /// <summary>
        /// If someone is data-bound to me, update them.
        /// </summary>
        private void ValueUpdate()
        {
            var binding = GetBindingExpression(ComboBox.TextProperty);
            if (binding != null)
            {
                binding.UpdateSource();
            }
        }

        internal TextBox GetTextBox()
        {
            if (m_textBox == null)
            {
                m_textBox = (TextBox)GetTemplateChild("PART_EditableTextBox");
            }

            return m_textBox;
        }

        private Brush m_origBackground;
        private bool m_hasFocus;
        private string m_selectedItem;
        private TextBox m_textBox;
        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PerfView
{
    public class EventCounterPoint
    {
        public double X { get; set; }
        public double Y { get; set; }

        public EventCounterPoint(double x, double y)
        {
            this.X = x;
            this.Y = y;
        }
    }

    /// <summary>
    /// Interaction logic for EventCounterWindow.xaml
    /// </summary>
    public partial class EventCounterWindow : Window
    {
        private ObservableCollection<EventCounterPoint> eventPoints;

        public EventCounterWindow()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        public ObservableCollection<EventCounterPoint> EventPoints
        {
            get
            {
                if (this.eventPoints == null)
                {
                    this.eventPoints = new ObservableCollection<EventCounterPoint>();
                }

                return this.eventPoints;
            }
        }

    }
}

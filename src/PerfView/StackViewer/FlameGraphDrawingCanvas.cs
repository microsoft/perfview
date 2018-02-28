using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using static PerfView.FlameGraph;

namespace PerfView
{
    public class FlameGraphDrawingCanvas : Canvas
    {
        private static readonly Brush[] Brushes = GenerateBrushes(new Random(12345));

        private List<Visual> visuals = new List<Visual>();

        protected override int VisualChildrenCount => visuals.Count;

        protected override Visual GetVisualChild(int index) => visuals[index];

        public bool IsEmpty => visuals.Count == 0;

        public void Draw(IEnumerable<FlameBox> boxes)
        {
            for (int i = visuals.Count - 1; i >= 0; i--)
            {
                DeleteVisual(visuals[i]);
                visuals.RemoveAt(i);
            }

            var visual = new DrawingVisual();

            using (DrawingContext drawingContext = visual.RenderOpen())
            {
                int index = 0;
                foreach (var box in boxes)
                {
                    var brush = Brushes[index++ % Brushes.Length];

                    drawingContext.DrawRectangle(
                        brush, 
                        null,  // if we provide any Pen the drawing process becomes very slow for large data sets
                        new System.Windows.Rect(box.X, box.Y, box.Width, box.Height));
                }

                AddVisual(visual);
            }
        }

        private void AddVisual(Visual visual)
        {
            visuals.Add(visual);

            base.AddVisualChild(visual);
            base.AddLogicalChild(visual);
        }

        private void DeleteVisual(Visual visual)
        {
            base.RemoveVisualChild(visual);
            base.RemoveLogicalChild(visual);
        }

        private static Brush[] GenerateBrushes(Random random)
            => Enumerable.Range(0, 100)
                    .Select(_ => (Brush)new SolidColorBrush(
                        Color.FromRgb(
                            (byte)(205.0 + 50.0 * random.NextDouble()),
                            (byte)(230.0 * random.NextDouble()),
                            (byte)(55.0 * random.NextDouble()))))
                    .ToArray();
    }
}

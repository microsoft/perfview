using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using static PerfView.FlameGraph;

namespace PerfView
{
    public class FlameGraphDrawingCanvas : Canvas
    {
        private static readonly Typeface Typeface = new Typeface("Consolas");

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
                        null,  // this is crucial for performance
                        new System.Windows.Rect(box.X, box.Y, box.Width, box.Height));

                    if (box.Width > 50 && box.Height >= 6) // we draw the text only if humans can see something
                    {
                        var text = new FormattedText(
                                box.Node.DisplayName,
                                CultureInfo.InvariantCulture,
                                System.Windows.FlowDirection.LeftToRight,
                                Typeface,
                                Math.Min(box.Height, 20),
                                System.Windows.Media.Brushes.Black);

                        text.MaxTextWidth = box.Width;
                        text.MaxTextHeight = box.Height;

                        drawingContext.DrawText(text, new System.Windows.Point(box.X, box.Y));
                    }
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
        {
            var brushes = Enumerable.Range(0, 100)
                    .Select(_ => (Brush)new SolidColorBrush(
                        Color.FromRgb(
                            (byte)(205.0 + 50.0 * random.NextDouble()),
                            (byte)(230.0 * random.NextDouble()),
                            (byte)(55.0 * random.NextDouble()))))
                    .ToArray();

            foreach (var brush in brushes)
                brush.Freeze(); // this is crucial for performance

            return brushes;
        }
    }
}

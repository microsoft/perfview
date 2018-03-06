using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Diagnostics.Tracing.Stacks;
using static PerfView.FlameGraph;

namespace PerfView
{
    public class FlameGraphDrawingCanvas : Canvas
    {
        private static readonly Typeface Typeface = new Typeface("Consolas");

        private static readonly Brush[] Brushes = GenerateBrushes(new Random(12345));

        public event EventHandler<string> CurrentFlameBoxChanged;

        private List<Visual> visuals = new List<Visual>();
        private FlameBoxesMap flameBoxesMap = new FlameBoxesMap();
        private ToolTip tooltip = new ToolTip();
        private CallTreeNode selectedNode;

        public FlameGraphDrawingCanvas()
        {
            MouseMove += OnMouseMove;
            MouseLeave += (s, e) => HideTooltip();
            MouseRightButtonDown += (s, e) => selectedNode = flameBoxesMap.Find(e.MouseDevice.GetPosition(this)).Node;
        }

        public bool IsEmpty => visuals.Count == 0;

        public CallTreeNodeBase SelectedNode => selectedNode;

        protected override int VisualChildrenCount => visuals.Count;

        protected override Visual GetVisualChild(int index) => visuals[index];

        public void Draw(IEnumerable<FlameBox> boxes)
        {
            Clear();

            var visual = new DrawingVisual(); // we have only one visual to provide best possible perf

            using (DrawingContext drawingContext = visual.RenderOpen())
            {
                int index = 0;
                System.Drawing.Font forSize = null; 

                foreach (var box in boxes)
                {
                    var brush = Brushes[index++ % Brushes.Length];

                    drawingContext.DrawRectangle(
                        brush,
                        null,  // no Pen is crucial for performance
                        new Rect(box.X, box.Y, box.Width, box.Height));

                    if (box.Width > 50 && box.Height >= 6) // we draw the text only if humans can see something
                    {
                        if (forSize == null)
                            forSize = new System.Drawing.Font("Consolas", (float)box.Height, System.Drawing.GraphicsUnit.Pixel);

                        var text = new FormattedText(
                                box.Node.DisplayName,
                                CultureInfo.InvariantCulture,
                                FlowDirection.LeftToRight,
                                Typeface,
                                Math.Min(forSize.SizeInPoints, 20),
                                System.Windows.Media.Brushes.Black);

                        text.MaxTextWidth = box.Width;
                        text.MaxTextHeight = box.Height;

                        drawingContext.DrawText(text, new Point(box.X, box.Y));
                    }

                    flameBoxesMap.Add(box);
                }

                AddVisual(visual);

                flameBoxesMap.Sort();
            }
        }

        /// <summary>
        /// DrawingVisual provides no tooltip support, so I had to implement it myself.. I feel bad for it.
        /// </summary>
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!IsEmpty)
            {
                var position = Mouse.GetPosition(this);
                var tooltipText = flameBoxesMap.Find(position).TooltipText;
                if (tooltipText != null)
                {
                    ShowTooltip(tooltipText);
                    CurrentFlameBoxChanged(this, tooltipText);
                    return;
                }
            }

            HideTooltip();
        }

        private void ShowTooltip(string text)
        {
            if (object.ReferenceEquals(tooltip.Content, text) && tooltip.IsOpen)
                return;

            tooltip.IsOpen = false; // by closing and opening it again we restart it's position to the current mouse position..
            tooltip.Content = text;
            tooltip.Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse;
            tooltip.IsOpen = true;
            tooltip.PlacementTarget = this;
        }

        private void HideTooltip() => tooltip.IsOpen = false;

        private void Clear()
        {
            for (int i = visuals.Count - 1; i >= 0; i--)
            {
                DeleteVisual(visuals[i]);
                visuals.RemoveAt(i);
            }

            flameBoxesMap.Clear();
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

        private class FlameBoxesMap
        {
            SortedDictionary<Range, List<FlameBox>> boxesMap = new SortedDictionary<Range, List<FlameBox>>();

            internal void Clear() => boxesMap.Clear();

            internal void Add(FlameBox flameBox)
            {
                var row = new Range(flameBox.Y, flameBox.Y + flameBox.Height);

                if (!boxesMap.TryGetValue(row, out var list))
                    boxesMap.Add(row, list = new List<FlameBox>());

                list.Add(flameBox);
            }

            internal void Sort()
            {
                foreach (var row in boxesMap.Values)
                    row.Sort(CompareByX); // sort the boxes from left to the right
            }

            internal FlameBox Find(Point point)
            {
                foreach (var rowData in boxesMap)
                    if (rowData.Key.Contains(point.Y))
                    {
                        int low = 0, high = rowData.Value.Count - 1, mid = 0;

                        while (low <= high)
                        {
                            mid = (low + high) / 2;

                            if (rowData.Value[mid].X > point.X)
                                high = mid - 1;
                            else if ((rowData.Value[mid].X + rowData.Value[mid].Width) < point.X)
                                low = mid + 1;
                            else
                                break;
                        }

                        if (rowData.Value[mid].X <= point.X && point.X <= (rowData.Value[mid].X + rowData.Value[mid].Width))
                            return rowData.Value[mid];

                        return default(FlameBox);
                    }

                return default(FlameBox);
            }

            private static int CompareByX(FlameBox left, FlameBox right) => left.X.CompareTo(right.X);

            struct Range : IEquatable<Range>, IComparable<Range>
            {
                private readonly double Start, End;

                internal Range(double start, double end)
                {
                    Start = start;
                    End = end;
                }

                internal bool Contains(double y) => Start <= y && y <= End;

                public override bool Equals(object obj) => throw new InvalidOperationException("No boxing");

                public bool Equals(Range other) => other.Start == Start && other.End == End;

                public int CompareTo(Range other) => other.Start.CompareTo(Start);

                public override int GetHashCode() => (Start * End).GetHashCode();
            }
        }
    }
}

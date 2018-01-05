using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace PerfView
{
    public static class FlameGraph
    {
        private static readonly FontFamily FontFamily = new FontFamily("Consolas");
        private static readonly Brush[] Brushes = GenerateBrushes(new Random(12345));

        /// <summary>
        /// (X=0, Y=0) is the left bottom corner of the canvas
        /// </summary>
        public struct FlameBox
        {
            public readonly double Width, Height, X, Y;
            public readonly CallTreeNode Node;

            public FlameBox(CallTreeNode node, double width, double height, double x, double y)
            {
                Node = node;
                Width = width;
                Height = height;
                X = x;
                Y = y;
            }
        }

        private struct FlamePair
        {
            public readonly FlameBox ParentBox;
            public readonly CallTreeNode Node;

            public FlamePair(FlameBox parentBox, CallTreeNode node)
            {
                ParentBox = parentBox;
                Node = node;
            }
        }

        public static IEnumerable<FlameBox> Calculate(CallTree callTree, double maxWidth, double maxHeight)
        {
            double maxDepth = GetMaxDepth(callTree.Root);
            double boxHeight = maxHeight / maxDepth;
            double pixelsPerIncusiveSample = maxWidth / callTree.Root.InclusiveMetric;

            var rootBox = new FlameBox(callTree.Root, maxWidth, boxHeight, 0, 0);
            yield return rootBox;

            var nodesToVisit = new Queue<FlamePair>();
            nodesToVisit.Enqueue(new FlamePair(rootBox, callTree.Root));

            while (nodesToVisit.Count > 0)
            {
                var current = nodesToVisit.Dequeue();
                var parentBox = current.ParentBox;
                var currentNode = current.Node;

                double nextBoxX = (parentBox.Width - (currentNode.Callees.Sum(child => child.InclusiveMetric) * pixelsPerIncusiveSample)) / 2.0; // centering the starting point

                foreach (var child in currentNode.Callees)
                {
                    double childBoxWidth = child.InclusiveMetric * pixelsPerIncusiveSample;
                    var childBox = new FlameBox(child, childBoxWidth, boxHeight, parentBox.X + nextBoxX, parentBox.Y + boxHeight);
                    nextBoxX += childBoxWidth;

                    if (child.Callees != null)
                        nodesToVisit.Enqueue(new FlamePair(childBox, child));

                    yield return childBox;
                }
            }
        }

        public static void Draw(IEnumerable<FlameBox> boxes, Canvas canvas)
        {
            canvas.Children.Clear();

            int index = 0;
            foreach (var box in boxes)
            {
                FrameworkElement rectangle = CreateRectangle(box, ++index);

                Canvas.SetLeft(rectangle, box.X);
                Canvas.SetBottom(rectangle, box.Y);
                canvas.Children.Add(rectangle);
            }
        }
        
        public static void Export(Canvas flameGraphCanvas, string filePath)
        {
            var rectangle = new Rect(flameGraphCanvas.RenderSize);
            var renderTargetBitmap = new RenderTargetBitmap((int)rectangle.Right, (int)rectangle.Bottom, 96d, 96d, PixelFormats.Default);
            renderTargetBitmap.Render(flameGraphCanvas);

            var pngEncoder = new PngBitmapEncoder();
            pngEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

            using (var file = System.IO.File.Create(filePath))
                pngEncoder.Save(file);
        }

        private static Brush[] GenerateBrushes(Random random)
            => Enumerable.Range(0, 100)
                    .Select(_ => (Brush)new SolidColorBrush(
                        Color.FromRgb(
                            (byte)(205.0 + 50.0 * random.NextDouble()),
                            (byte)(230.0 * random.NextDouble()),
                            (byte)(55.0 * random.NextDouble()))))
                    .ToArray();

        private static double GetMaxDepth(CallTreeNode callTree)
        {
            double deepest = 0;

            if (callTree.Callees != null)
                foreach (var callee in callTree.Callees)
                    deepest = Math.Max(deepest, GetMaxDepth(callee));

            return deepest + 1;
        }

        private static FrameworkElement CreateRectangle(FlameBox box, int index)
        {
            var tooltip = $"Method: {box.Node.DisplayName} ({box.Node.InclusiveCount} inclusive samples, {box.Node.InclusiveMetricPercent:F}%)";
            var background = Brushes[++index % Brushes.Length]; // in the future, the color could be chosen according to the belonging of the method (JIT, GC, user code, OS etc)

            // for small boxes we create Rectangles, because they are much faster (too many TextBlocks === bad perf) 
            // also for small rectangles it's impossible to read the name of the method anyway (only few characters are printed)
            if (box.Width < 50) 
                return new Rectangle
                {
                    Height = box.Height,
                    Width = box.Width,
                    Fill = background, 
                    ToolTip = new ToolTip { Content = tooltip },
                    DataContext = box.Node
                };

            return new TextBlock
            {
                Height = box.Height,
                Width = box.Width,
                Background = background,
                ToolTip = new ToolTip { Content = tooltip },
                Text = box.Node.DisplayName,
                DataContext = box.Node,
                FontFamily = FontFamily,
                FontSize = Math.Min(20.0, box.Height)
            };
        }
    }
}
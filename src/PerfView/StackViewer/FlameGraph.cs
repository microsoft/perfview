using Microsoft.Diagnostics.Tracing.Stacks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PerfView
{
    public static class FlameGraph
    {
        /// <summary>
        /// (X=0, Y=0) is the left upper corner of the canvas
        /// </summary>
        public struct FlameBox
        {
            public readonly double Width, Height, X, Y;
            public readonly CallTreeNode Node;
            public string TooltipText;

            public FlameBox(CallTreeNode node, double width, double height, double x, double y)
            {
                Node = node;
                TooltipText = $"Method: {node.DisplayName} ({node.InclusiveCount} inclusive samples, {node.InclusiveMetricPercent:F}%){(node.InclusiveCount < 0 ? " (Memory gain)" : string.Empty)}";
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
            double pixelsPerIncusiveSample = maxWidth / Math.Abs(callTree.Root.InclusiveMetric);

            var rootBox = new FlameBox(callTree.Root, maxWidth, boxHeight, 0, maxHeight - boxHeight);
            yield return rootBox;

            var nodesToVisit = new Queue<FlamePair>();
            nodesToVisit.Enqueue(new FlamePair(rootBox, callTree.Root));

            while (nodesToVisit.Count > 0)
            {
                var current = nodesToVisit.Dequeue();
                var parentBox = current.ParentBox;
                var currentNode = current.Node;

                double nextBoxX = (parentBox.Width - (currentNode.Callees.Sum(child => Math.Abs(child.InclusiveMetric)) * pixelsPerIncusiveSample)) / 2.0; // centering the starting point

                foreach (var child in currentNode.Callees)
                {
                    double childBoxWidth = Math.Abs(child.InclusiveMetric) * pixelsPerIncusiveSample;

                    var childBox = new FlameBox(child, childBoxWidth, boxHeight, parentBox.X + nextBoxX, parentBox.Y - boxHeight);
                    nextBoxX += childBoxWidth;

                    if (child.Callees != null)
                    {
                        nodesToVisit.Enqueue(new FlamePair(childBox, child));
                    }

                    yield return childBox;
                }
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
            {
                pngEncoder.Save(file);
            }
        }

        private static double GetMaxDepth(CallTreeNode callTree)
        {
            double deepest = 0;

            if (callTree.Callees != null)
            {
                foreach (var callee in callTree.Callees)
                {
                    deepest = Math.Max(deepest, GetMaxDepth(callee));
                }
            }

            return deepest + 1;
        }
    }
}
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PerfView
{
    static class ScreenShot
    {
        /// <summary>
        /// Take a screenshot of the entire desktop. 
        /// </summary>
        public static void TakeDesktopScreenShot(string fileName)
        {
            var bmpScreenshot = new Bitmap(
                System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // Create a graphics object from the bitmap
            var gfxScreenshot = Graphics.FromImage(bmpScreenshot);

            // Take the screenshot from the upper left corner to the right bottom corner

            gfxScreenshot.CopyFromScreen(
                System.Windows.Forms.Screen.PrimaryScreen.Bounds.X,
                System.Windows.Forms.Screen.PrimaryScreen.Bounds.Y, 0, 0,
                System.Windows.Forms.Screen.PrimaryScreen.Bounds.Size, CopyPixelOperation.SourceCopy);

            bmpScreenshot.Save(fileName, System.Drawing.Imaging.ImageFormat.Png);
            return;
        }

        /// <summary>
        /// Take a screenshot of a particular window (WPF visual)
        /// </summary>
        public static void TakeScreenShotOfVisual(string fileName, Window target)
        {
            var bitmap = new RenderTargetBitmap((int)target.Width, (int)target.Height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(target);
            var png = new PngBitmapEncoder();

            png.Frames.Add(BitmapFrame.Create(bitmap));
            using (Stream stream = File.Create("Screenshot.png"))
                png.Save(stream);
        }

    }
}
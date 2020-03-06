using System.IO;

using static Microsoft.Diagnostics.Tracing.Stacks.StackSourceWriterHelper;

namespace Microsoft.Diagnostics.Tracing.Stacks.Formats
{
    public class ChromiumStackSourceWriter
    {
        /// <summary>
        /// exports provided StackSource to a Chromium Trace File format 
        /// schema: https://docs.google.com/document/d/1CvAClvFfyA5R-PhYUmn5OOQtYMH4h6I0nSsKchNAySU/
        /// </summary>
        public static void WriteStackViewAsJson(StackSource source, string filePath)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);

            using (var writeStream = File.CreateText(filePath))
                Export(source, writeStream, Path.GetFileNameWithoutExtension(filePath));
        }

        #region private
        internal static void Export(StackSource source, TextWriter writer, string name)
        {
            var samplesPerThread = GetSortedSamplesPerThread(source);
        }
        #endregion private
    }
}
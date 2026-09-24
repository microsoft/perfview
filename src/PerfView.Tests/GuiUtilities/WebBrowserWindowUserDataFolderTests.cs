using System;
using System.IO;
using PerfView.GuiUtilities;
using Xunit;

namespace PerfViewTests.GuiUtilities
{
    public class WebBrowserWindowUserDataFolderTests
    {
        [Theory]
        [InlineData(@"C:\Users\Test\AppData\Local", @"C:\Users\Test\AppData\Local\PerfView\WebView2")]
        [InlineData(@"C:\Users\Test User\AppData\Local", @"C:\Users\Test User\AppData\Local\PerfView\WebView2")]
        [InlineData("C:\\Users\\T\u00e9st\\AppData\\Local", "C:\\Users\\T\u00e9st\\AppData\\Local\\PerfView\\WebView2")]
        [InlineData(@"C:\Users\Test\AppData\Local\", @"C:\Users\Test\AppData\Local\PerfView\WebView2")]
        [InlineData(@"D:\LocalData", @"D:\LocalData\PerfView\WebView2")]
        public void UserDataFolderIsUnderLocalApplicationData(string localApplicationData, string expected)
        {
            Assert.Equal(expected, WebBrowserWindow.GetUserDataFolder(localApplicationData));
        }

        [Fact]
        public void UserDataFolderUsesWindowsLocalApplicationData()
        {
            string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            Assert.False(string.IsNullOrEmpty(localApplicationData));
            Assert.Equal(
                Path.Combine(localApplicationData, "PerfView", "WebView2"),
                WebBrowserWindow.GetUserDataFolder(localApplicationData));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("LocalData")]
        [InlineData(@".\LocalData")]
        [InlineData(@"..\LocalData")]
        [InlineData(@"C:LocalData")]
        [InlineData("C:")]
        [InlineData(@"\LocalData")]
        [InlineData("/LocalData")]
        public void MissingOrRelativeLocalApplicationDataIsRejected(string localApplicationData)
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => WebBrowserWindow.GetUserDataFolder(localApplicationData));

            Assert.Contains("LocalApplicationData", exception.Message);
        }
    }
}

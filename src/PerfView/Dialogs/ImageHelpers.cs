using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using System.Windows.Interop;
using System.Runtime.CompilerServices;
using System;

namespace PerfView.Dialogs;

internal static class ImageHelpers
{
    /// <summary>
    ///  Gets the <see cref="ImageSource"/> for the specified <see cref="MessageBoxImage"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This method reurns the modern version of the stock icons used in message boxes.
    ///  </para>
    /// </remarks>
    public static ImageSource ToImageSource(MessageBoxImage image) => image switch
    {
        MessageBoxImage.Error => GetStockIcon(SHSTOCKICONID.SIID_ERROR),
        MessageBoxImage.Information => GetStockIcon(SHSTOCKICONID.SIID_INFO),
        MessageBoxImage.Warning => GetStockIcon(SHSTOCKICONID.SIID_WARNING),
        MessageBoxImage.Question => GetStockIcon(SHSTOCKICONID.SIID_HELP),
        _ => throw new ArgumentOutOfRangeException(nameof(image)),
    };

    private static unsafe ImageSource GetStockIcon(SHSTOCKICONID stockIcon, SHGSI_FLAGS options = default)
    {
        // Note that we don't explicitly check for invalid StockIconId to allow for accessing newer ids introduced
        // in later OSes. The HRESULT returned for undefined ids gets converted to an ArgumentException.

        SHSTOCKICONINFO info = new()
        {
            cbSize = (uint)Unsafe.SizeOf<SHSTOCKICONINFO>(),
        };

        HRESULT result = SHGetStockIconInfo(stockIcon, options | SHGSI_FLAGS.SHGSI_ICON, &info);

        // This only throws if there is an error.
        Marshal.ThrowExceptionForHR((int)result);

        return Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
    }

    // This can't be imported in CsWin32 as it technically isn't the same on both X86 and X64 due to a packing of 1 byte on X86.
    // For our purposes this is fine as the single definition's layout (SHSTOCKICONINFO) is the same on both platforms.

    [DllImport("Shell32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern unsafe HRESULT SHGetStockIconInfo(SHSTOCKICONID siid, SHGSI_FLAGS uFlags, SHSTOCKICONINFO* psii);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private unsafe struct SHSTOCKICONINFO
    {
        public uint cbSize;
        public HICON hIcon;
        public int iSysImageIndex;
        public int iIcon;
        public fixed char szPath[260];
    }
}

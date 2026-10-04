using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AccIntegrationWPF;

internal static class NativeWindowDrag
{
    private const uint WmNcLeftButtonDown = 0x00A1;
    private const int HtCaption = 2;
    private const int SmSwapButton = 23;

    public static void Start(Window window)
    {
        // WebView2 owns a native child window; WPF's mouse state is not the input source.
        // Ignore a delayed bridge message if the primary mouse button was already released.
        int primaryButton = GetSystemMetrics(SmSwapButton) == 0 ? 0x01 : 0x02;
        if (GetAsyncKeyState(primaryButton) >= 0 || !window.IsVisible)
            return;

        nint handle = new WindowInteropHelper(window).Handle;
        if (handle == 0 || !GetCursorPos(out Point cursor))
            return;

        // Pack signed screen coordinates, including monitors left/above the primary display.
        nint position = (nint)((cursor.X & 0xffff) | ((cursor.Y & 0xffff) << 16));
        ReleaseCapture();
        SendMessage(handle, WmNcLeftButtonDown, HtCaption, position);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);
}

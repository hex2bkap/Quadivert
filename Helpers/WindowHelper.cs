using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Quadivert.Models;

namespace Quadivert.Helpers;

public static class WindowHelper
{
    private delegate IntPtr SubclassProc(
        IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
        IntPtr uIdSubclass, IntPtr dwRefData);

    private static readonly Dictionary<IntPtr, SubclassProc> _subclassProcs = new();

    [DllImport("Comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(
        IntPtr hWnd, SubclassProc pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("Comctl32.dll")]
    private static extern IntPtr DefSubclassProc(
        IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    private const uint WM_GETMINMAXINFO = 0x0024;

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    public static void SetMinSize(Microsoft.UI.Xaml.Window window, int minWidthLogical, int minHeightLogical)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        uint dpi = GetDpiForWindow(hwnd);
        int minW = (int)(minWidthLogical * dpi / 96.0);
        int minH = (int)(minHeightLogical * dpi / 96.0);

        SubclassProc proc = (hWnd, uMsg, wParam, lParam, _, _) =>
        {
            if (uMsg == WM_GETMINMAXINFO)
            {
                var info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                if (info.MinTrackSize.X < minW) info.MinTrackSize.X = minW;
                if (info.MinTrackSize.Y < minH) info.MinTrackSize.Y = minH;
                Marshal.StructureToPtr(info, lParam, false);
                return IntPtr.Zero;
            }
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        };

        _subclassProcs[hwnd] = proc;
        SetWindowSubclass(hwnd, proc, (IntPtr)1, IntPtr.Zero);
    }

    public static void RestorePosition(Microsoft.UI.Xaml.Window window, AppSettings settings)
    {
        var appWin = window.AppWindow;
        if (settings.WindowWidth <= 0 || settings.WindowHeight <= 0) return;

        int w = Math.Max(settings.WindowWidth, 580);
        int h = Math.Max(settings.WindowHeight, 600);
        int x = settings.WindowX;
        int y = settings.WindowY;

        if (x < 0 && y < 0)
        {
            var primary = DisplayArea.Primary;
            x = (primary.WorkArea.Width - w) / 2 + primary.WorkArea.X;
            y = (primary.WorkArea.Height - h) / 2 + primary.WorkArea.Y;
        }
        else if (!IsTitleBarVisible(x, y, w))
        {
            var primary = DisplayArea.Primary;
            x = (primary.WorkArea.Width - w) / 2 + primary.WorkArea.X;
            y = (primary.WorkArea.Height - h) / 2 + primary.WorkArea.Y;
        }

        appWin.MoveAndResize(new RectInt32 { X = x, Y = y, Width = w, Height = h });
    }

    public static void SavePosition(Microsoft.UI.Xaml.Window window, AppSettings settings)
    {
        var appWin = window.AppWindow;
        if (appWin.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized })
            return;

        settings.WindowX = appWin.Position.X;
        settings.WindowY = appWin.Position.Y;
        settings.WindowWidth = appWin.Size.Width;
        settings.WindowHeight = appWin.Size.Height;
    }

    private static bool IsTitleBarVisible(int x, int y, int w)
    {
        const int TitleBarHeight = 50;
        const int MinVisibleWidth = 50;
        var areas = DisplayArea.FindAll();
        for (int i = 0; i < areas.Count; i++)
        {
            var wa = areas[i].WorkArea;
            bool verticallyVisible = y >= wa.Y - TitleBarHeight && y < wa.Y + wa.Height;
            bool horizontallyVisible = x + w > wa.X + MinVisibleWidth && x < wa.X + wa.Width - MinVisibleWidth;
            if (verticallyVisible && horizontallyVisible) return true;
        }
        return false;
    }
}

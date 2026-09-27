using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace DesktopStats;

/// <summary>
/// The overlay: a per-pixel-alpha layered window sized to the panel, drawn behind every
/// other window.
///
/// It used to be a full-screen form using a TransparencyKey. A colour key is binary - a
/// pixel is either the key and fully transparent, or fully opaque - so every antialiased
/// edge pixel of the text and gauges, having been blended against the near-black key,
/// stayed on screen as a dark fringe. Invisible on a dark wallpaper, jagged on a light
/// one. UpdateLayeredWindow composites a premultiplied 32-bit bitmap with real alpha, so
/// edges and the translucent shadows blend with whatever wallpaper is underneath.
///
/// Layout is written in 96-DPI units and scaled by the monitor's DPI, so the panel keeps
/// the same physical size and stays sharp at 100%, 125%, 150% and so on, whatever the
/// resolution. The process is per-monitor DPI aware (ApplicationHighDpiMode in the
/// csproj), so the scale follows changes to Windows display scaling and RDP sessions
/// that connect at a different DPI.
/// </summary>
public class OverlayForm : Form
{
    // Null while this session is disconnected. See OnSessionSwitch.
    private SensorReader? _sensors;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly System.Windows.Forms.Timer _zTimer;
    private SystemSnapshot _snapshot = new();
    private Surface? _surface;

    // ── Win32 ──

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref NativePoint pptDst,
        ref NativeSize psize, IntPtr hdcSrc, ref NativePoint pptSrc, int crKey,
        ref BlendFunction pblend, int dwFlags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr ho);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfoHeader pbmi,
        uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize { public int Cx, Cy; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace DesktopStats;

public class OverlayForm : Form
{
    private readonly SensorReader _sensors;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly System.Windows.Forms.Timer _zTimer;
    private SystemSnapshot _snapshot = new();
    private Bitmap _backBuffer;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    // ── Layout ──
    private readonly int _panelMarginRight = 30;
    private readonly int _panelMarginTop = 40;
    private readonly int _panelWidth = 300;
    private readonly int _refreshMs = 1000;
    private readonly string _fontFamily = "Segoe UI";

    private static readonly Color _transpKey = Color.FromArgb(255, 1, 1, 1);

    // ── Wallpaper-derived theme ──
    private Color _accent = Color.FromArgb(255, 80, 160, 255);
    private Color _accentDim = Color.FromArgb(140, 60, 120, 200);
    private Color _textBright = Color.FromArgb(230, 240, 240, 240);
    private Color _textMid = Color.FromArgb(170, 220, 220, 220);
    private Color _textDim = Color.FromArgb(100, 200, 200, 200);
    private Color _shadow = Color.FromArgb(140, 0, 0, 0);
    private Color _barBg = Color.FromArgb(40, 255, 255, 255);

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        DoubleBuffered = true;

        var screen = Screen.PrimaryScreen!;
        Location = Point.Empty;
        Size = screen.Bounds.Size;

        BackColor = _transpKey;
        TransparencyKey = _transpKey;

        _backBuffer = new Bitmap(Width, Height);
        _sensors = new SensorReader();

        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        AnalyzeWallpaper();

        _timer = new System.Windows.Forms.Timer { Interval = _refreshMs };
        _timer.Tick += (_, _) =>
        {
            try { _snapshot = _sensors.Read(); } catch { }
            RenderToBuffer();
            Invalidate();
        };
        _timer.Start();

        _zTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _zTimer.Tick += (_, _) => SendToBottom();
        _zTimer.Start();

        try { _snapshot = _sensors.Read(); } catch { }
        RenderToBuffer();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        SendToBottom();
    }

    private void SendToBottom()
    {
        SetWindowPos(Handle, HWND_BOTTOM, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(OnDisplaySettingsChanged, sender, e);
            return;
        }

        var screen = Screen.PrimaryScreen!;
        Location = Point.Empty;
        Size = screen.Bounds.Size;

        _backBuffer.Dispose();
        _backBuffer = new Bitmap(Width, Height);

        AnalyzeWallpaper();
        RenderToBuffer();
        Invalidate();
    }

    // ── Wallpaper color extraction ──

    private void AnalyzeWallpaper()
    {
        try
        {
            string? path = null;
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
            {
                path = key?.GetValue("Wallpaper") as string;
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            Console.WriteLine($"Analyzing wallpaper: {path}");

            using var wallpaper = Image.FromFile(path);
            using var small = new Bitmap(200, 120);
            using (var g = Graphics.FromImage(small))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.DrawImage(wallpaper, 0, 0, 200, 120);
            }

            float totalBrightness = 0;
            int totalSamples = 0;

            // Collect saturated colors with their frequency
            // Use hue buckets (36 buckets of 10 degrees each)
            float[] hueBucketR = new float[36];
            float[] hueBucketG = new float[36];
            float[] hueBucketB = new float[36];
            float[] hueBucketSat = new float[36];
            int[] hueBucketCount = new int[36];

            int startX = small.Width * 2 / 3; // right third where panel lives

            for (int y = 0; y < small.Height; y++)
            {
                for (int x = startX; x < small.Width; x++)
                {
                    var pixel = small.GetPixel(x, y);
                    totalBrightness += pixel.GetBrightness();
                    totalSamples++;

                    float sat = pixel.GetSaturation();
                    float bri = pixel.GetBrightness();

                    // Only count sufficiently saturated and visible pixels
                    if (sat > 0.1f && bri > 0.1f && bri < 0.9f)
                    {
                        float hue = pixel.GetHue(); // 0-360
                        int bucket = Math.Clamp((int)(hue / 10f), 0, 35);
                        hueBucketR[bucket] += pixel.R;
                        hueBucketG[bucket] += pixel.G;
                        hueBucketB[bucket] += pixel.B;
                        hueBucketSat[bucket] += sat;
                        hueBucketCount[bucket]++;
                    }
                }
            }

            if (totalSamples == 0) return;

            float avgBrightness = totalBrightness / totalSamples;
            bool darkBg = avgBrightness < 0.5f;

            // Find the bucket with the most saturated pixels (weighted by count * avg saturation)
            int bestBucket = -1;
            float bestScore = 0;
            for (int i = 0; i < 36; i++)
            {
                if (hueBucketCount[i] < 3) continue; // ignore noise
                float avgSat = hueBucketSat[i] / hueBucketCount[i];
                float score = hueBucketCount[i] * avgSat; // frequency * saturation
                if (score > bestScore)
                {
                    bestScore = score;
                    bestBucket = i;
                }
            }

            float accentR, accentG, accentB;
            if (bestBucket >= 0 && hueBucketCount[bestBucket] > 0)
            {
                // Average the colors in the winning bucket
                int c = hueBucketCount[bestBucket];
                accentR = hueBucketR[bestBucket] / c;
                accentG = hueBucketG[bestBucket] / c;
                accentB = hueBucketB[bestBucket] / c;
            }
            else
            {
                // Fallback: average all pixels
                float aR = 0, aG = 0, aB = 0;
                int cnt = 0;
                for (int y = 0; y < small.Height; y++)
                {
                    for (int x = startX; x < small.Width; x++)
                    {
                        var p = small.GetPixel(x, y);
                        aR += p.R; aG += p.G; aB += p.B; cnt++;
                    }
                }
                accentR = cnt > 0 ? aR / cnt : 80;
                accentG = cnt > 0 ? aG / cnt : 160;
                accentB = cnt > 0 ? aB / cnt : 255;
            }

            // Boost saturation and adjust lightness for the accent
            float h, s, l;
            ColorToHSL(accentR / 255f, accentG / 255f, accentB / 255f, out h, out s, out l);
            s = Math.Clamp(s * 1.3f, 0.4f, 0.9f);
            l = darkBg ? Math.Clamp(l, 0.45f, 0.65f) : Math.Clamp(l, 0.35f, 0.55f);
            HSLToColor(h, s, l, out float fr, out float fg, out float fb);

            _accent = Color.FromArgb(255, Clamp255(fr), Clamp255(fg), Clamp255(fb));

            HSLToColor(h, s * 0.6f, l * 0.6f, out float dr, out float dg, out float db);
            _accentDim = Color.FromArgb(140, Clamp255(dr), Clamp255(dg), Clamp255(db));

            if (darkBg)
            {
                _textBright = Color.FromArgb(230, 240, 240, 240);
                _textMid = Color.FromArgb(170, 220, 220, 220);
                _textDim = Color.FromArgb(100, 200, 200, 200);
                _shadow = Color.FromArgb(140, 0, 0, 0);
                _barBg = Color.FromArgb(40, 255, 255, 255);
            }
            else
            {
                _textBright = Color.FromArgb(230, 20, 20, 20);
                _textMid = Color.FromArgb(170, 40, 40, 40);
                _textDim = Color.FromArgb(100, 60, 60, 60);
                _shadow = Color.FromArgb(80, 255, 255, 255);
                _barBg = Color.FromArgb(40, 0, 0, 0);
            }

            Console.WriteLine($"Theme: accent=({_accent.R},{_accent.G},{_accent.B}), hue={h * 360:F0}°, dark={darkBg}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Wallpaper analysis failed: {ex.Message}");
        }
    }

    private static int Clamp255(float f) => Math.Clamp((int)(f * 255), 0, 255);

    private static void ColorToHSL(float r, float g, float b, out float h, out float s, out float l)
    {
        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2f;
        if (max == min) { h = s = 0; return; }
        float d = max - min;
        s = l > 0.5f ? d / (2f - max - min) : d / (max + min);
        if (max == r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6f;
        else if (max == g) h = ((b - r) / d + 2) / 6f;
        else h = ((r - g) / d + 4) / 6f;
    }

    private static void HSLToColor(float h, float s, float l, out float r, out float g, out float b)
    {
        if (s == 0) { r = g = b = l; return; }
        float q = l < 0.5f ? l * (1 + s) : l + s - l * s;
        float p = 2 * l - q;
        r = HueToRGB(p, q, h + 1f / 3f);
        g = HueToRGB(p, q, h);
        b = HueToRGB(p, q, h - 1f / 3f);
    }

    private static float HueToRGB(float p, float q, float t)
    {
        if (t < 0) t += 1; if (t > 1) t -= 1;
        if (t < 1f / 6f) return p + (q - p) * 6f * t;
        if (t < 1f / 2f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
        return p;
    }

    // ── Rendering ──

    private void RenderToBuffer()
    {
        using var g = Graphics.FromImage(_backBuffer);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(_transpKey);
        DrawPanel(g);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImageUnscaled(_backBuffer, 0, 0);
    }

    private void DrawPanel(Graphics g)
    {
        var s = _snapshot;
        float panelX = Width - _panelMarginRight - _panelWidth;
        float y = _panelMarginTop;
        float w = _panelWidth;

        // ── CPU & GPU arc gauges side by side ──
        float gaugeSize = 100;
        float gaugeGap = 16;
        float totalGaugeW = gaugeSize * 2 + gaugeGap;
        float gx = panelX + (w - totalGaugeW) / 2f;

        DrawArcGauge(g, gx, y, gaugeSize, s.CpuLoadPercent ?? 0, "CPU");
        DrawArcGauge(g, gx + gaugeSize + gaugeGap, y, gaugeSize, s.GpuLoadPercent ?? 0, "GPU");

        y += gaugeSize + 2;

        // Sub-stats centered under each gauge
        float cpuCenterX = gx + gaugeSize / 2f;
        float gpuCenterX = gx + gaugeSize + gaugeGap + gaugeSize / 2f;

        DrawSubStats(g, cpuCenterX, y, s.CpuTempC, s.CpuPowerW);
        DrawSubStats(g, gpuCenterX, y, s.GpuTempC, s.GpuPowerW);
        y += 30;

        // ── VRAM ──
        if (s.GpuMemTotalMb.HasValue && s.GpuMemUsedMb.HasValue)
        {
            DrawCapacityBar(g, panelX, y, w, "VRAM",
                s.GpuMemUsedMb.Value / 1024f, s.GpuMemTotalMb.Value / 1024f, "GB");
            y += 36;
        }

        // ── RAM ──
        DrawCapacityBar(g, panelX, y, w, "RAM",
            s.RamUsedGb ?? 0, s.RamTotalGb, "GB");
        y += 36;

        // ── Storage ──
        foreach (var part in s.Partitions)
        {
            string label = string.IsNullOrEmpty(part.Label)
                ? part.Letter : $"{part.Letter} {part.Label}";
            DrawCapacityBar(g, panelX, y, w, label,
                (float)part.UsedGb, (float)part.TotalGb, "GB");
            y += 36;
        }

        // ── Network ──
        y += 4;
        DrawNetwork(g, panelX, y, w, s.NetDownBytesPerSec, s.NetUpBytesPerSec);
    }

    // ── Arc Gauge ──

    private void DrawArcGauge(Graphics g, float x, float y, float size,
        float percent, string label)
    {
        float pad = 10;
        float arcRect = size - pad * 2;
        float thickness = 5f;
        float ratio = Math.Clamp(percent / 100f, 0, 1);

        float startAngle = 135f;
        float sweepAngle = 270f;

        using var bgPen = new Pen(_barBg, thickness)
        { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(bgPen, x + pad, y + pad, arcRect, arcRect, startAngle, sweepAngle);

        if (ratio > 0.01f)
        {
            Color fill = GetGaugeColor(ratio);
            using var fillPen = new Pen(fill, thickness)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(fillPen, x + pad, y + pad, arcRect, arcRect,
                startAngle, sweepAngle * ratio);
        }

        string pctText = $"{percent:F0}%";
        using var pctFont = new Font(_fontFamily, 14f, FontStyle.Bold);
        var pctSize = g.MeasureString(pctText, pctFont);
        DrawShadow(g, pctText, pctFont, _textBright,
            x + (size - pctSize.Width) / 2f,
            y + (size - pctSize.Height) / 2f - 2);

        using var lblFont = new Font(_fontFamily, 8f, FontStyle.Bold);
        var lblSize = g.MeasureString(label, lblFont);
        DrawShadow(g, label, lblFont, _textDim,
            x + (size - lblSize.Width) / 2f,
            y + size - pad - 6);
    }

    private Color GetGaugeColor(float ratio)
    {
        if (ratio < 0.6f) return _accent;
        if (ratio < 0.8f)
            return LerpColor(_accent, Color.FromArgb(255, 255, 180, 50),
                (ratio - 0.6f) / 0.2f);
        return LerpColor(Color.FromArgb(255, 255, 180, 50),
            Color.FromArgb(255, 255, 60, 60), (ratio - 0.8f) / 0.2f);
    }

    // ── Sub-stats under gauges ──

    private void DrawSubStats(Graphics g, float centerX, float y,
        float? tempC, float? powerW)
    {
        using var font = new Font(_fontFamily, 7.5f, FontStyle.Bold);

        string tempText = tempC.HasValue ? $"{tempC:F0}°C" : "—";
        string powerText = powerW.HasValue ? $"{powerW:F0}W" : "—";

        Color tempColor = GetTempColor(tempC ?? 0);

        var tempSize = g.MeasureString(tempText, font);
        var powerSize = g.MeasureString(powerText, font);
        float gap = 10;
        float totalW = tempSize.Width + gap + powerSize.Width;
        float startX = centerX - totalW / 2f;

        DrawShadow(g, tempText, font, tempColor, startX, y);
        DrawShadow(g, powerText, font, _textDim, startX + tempSize.Width + gap, y);
    }

    private Color GetTempColor(float temp)
    {
        if (temp < 50) return _accent;
        if (temp < 70)
            return LerpColor(_accent, Color.FromArgb(255, 255, 180, 50),
                (temp - 50) / 20f);
        if (temp < 85)
            return LerpColor(Color.FromArgb(255, 255, 180, 50),
                Color.FromArgb(255, 255, 60, 60), (temp - 70) / 15f);
        return Color.FromArgb(255, 255, 60, 60);
    }

    // ── Capacity Bar ──

    private void DrawCapacityBar(Graphics g, float x, float y, float w,
        string label, float used, float total, string unit)
    {
        float barH = 6;
        float barY = y + 16;
        float ratio = total > 0 ? Math.Clamp(used / total, 0, 1) : 0;

        using var lblFont = new Font(_fontFamily, 8f, FontStyle.Bold);
        using var valFont = new Font(_fontFamily, 7.5f, FontStyle.Bold);

        DrawShadow(g, label, lblFont, _textMid, x, y);

        string valText = $"{used:F1} / {total:F1} {unit}";
        var valSize = g.MeasureString(valText, valFont);
        DrawShadow(g, valText, valFont, _textMid, x + w - valSize.Width, y);

        using var bgBrush = new SolidBrush(_barBg);
        FillRoundedRect(g, bgBrush, new RectangleF(x, barY, w, barH), 3);

        if (ratio > 0.01f)
        {
            Color fill = GetCapacityColor(ratio);
            using var fillBrush = new SolidBrush(fill);
            float fillW = Math.Max(barH, w * ratio);
            FillRoundedRect(g, fillBrush, new RectangleF(x, barY, fillW, barH), 3);
        }
    }

    private Color GetCapacityColor(float ratio)
    {
        var accentAlpha = Color.FromArgb(200, _accent.R, _accent.G, _accent.B);
        if (ratio < 0.7f) return accentAlpha;
        if (ratio < 0.85f)
            return LerpColor(accentAlpha, Color.FromArgb(200, 255, 180, 50),
                (ratio - 0.7f) / 0.15f);
        return LerpColor(Color.FromArgb(200, 255, 180, 50),
            Color.FromArgb(200, 255, 60, 60), (ratio - 0.85f) / 0.15f);
    }

    // ── Network ──

    private void DrawNetwork(Graphics g, float x, float y, float w,
        long down, long up)
    {
        using var lblFont = new Font(_fontFamily, 8f, FontStyle.Bold);
        using var valFont = new Font(_fontFamily, 8.5f, FontStyle.Bold);

        DrawShadow(g, "NETWORK", lblFont, _textDim, x, y);
        y += 20;

        DrawShadow(g, "▼", valFont, _accent, x, y);
        var arrowW = g.MeasureString("▼ ", valFont).Width;
        DrawShadow(g, $"{FormatBytes(down)}/s", valFont, _textMid, x + arrowW, y);

        string upText = $"{FormatBytes(up)}/s";
        var upSize = g.MeasureString(upText, valFont);
        var upArrowW = g.MeasureString("▲ ", valFont).Width;
        float upX = x + w - upSize.Width - upArrowW;
        DrawShadow(g, "▲", valFont, _accentDim, upX, y);
        DrawShadow(g, upText, valFont, _textMid, upX + upArrowW, y);
    }

    // ── Helpers ──

    private void DrawShadow(Graphics g, string text, Font font, Color color,
        float x, float y)
    {
        using var sBrush = new SolidBrush(_shadow);
        g.DrawString(text, font, sBrush, x + 1.2f, y + 1.2f);
        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, x, y);
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            a.A + (int)((b.A - a.A) * t),
            a.R + (int)((b.R - a.R) * t),
            a.G + (int)((b.G - a.G) * t),
            a.B + (int)((b.B - a.B) * t));
    }

    private static void FillRoundedRect(Graphics g, Brush brush, RectangleF rect, float radius)
    {
        if (rect.Width < 1 || rect.Height < 1) return;
        radius = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);
        using var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_000_000_000) return $"{bytes / 1_000_000_000.0:F1} GB";
        if (bytes >= 1_000_000) return $"{bytes / 1_000_000.0:F1} MB";
        if (bytes >= 1_000) return $"{bytes / 1_000.0:F1} KB";
        return $"{bytes} B";
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _timer.Stop();
        _zTimer.Stop();
        _sensors.Dispose();
        _backBuffer.Dispose();
        base.OnFormClosed(e);
    }
}

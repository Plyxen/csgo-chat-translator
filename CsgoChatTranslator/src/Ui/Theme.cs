using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace CsgoChatTranslator;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(20, 23, 28);
    public static readonly Color Surface = Color.FromArgb(32, 36, 43);
    public static readonly Color Border = Color.FromArgb(62, 69, 80);
    public static readonly Color Text = Color.FromArgb(242, 244, 247);
    public static readonly Color Muted = Color.FromArgb(160, 169, 181);
    public static readonly Color Accent = Color.FromArgb(126, 217, 160);
    public static readonly Color Warning = Color.FromArgb(240, 190, 100);
    public static readonly Color Error = Color.FromArgb(240, 120, 110);
    public static readonly Color Terrorist = Color.FromArgb(232, 178, 90);
    public static readonly Color CounterTerrorist = Color.FromArgb(111, 177, 255);

    public static Icon CreateAppIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            using var background = new SolidBrush(Background);
            using var accent = new SolidBrush(Accent);
            using var font = new Font("Segoe UI", 17f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.FillEllipse(background, 0, 0, 31, 31);
            g.DrawString("Я", font, accent, new RectangleF(0, 0, 32, 32), format);
        }

        var handle = bitmap.GetHicon();
        using var temporary = Icon.FromHandle(handle);
        var icon = (Icon)temporary.Clone();
        Native.DestroyIcon(handle);
        return icon;
    }
}

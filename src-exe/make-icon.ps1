# Generates app.ico from the DeepSeek whale base icon plus a small gear/wrench badge.
# Run from src-exe: powershell -NoProfile -ExecutionPolicy Bypass -File .\make-icon.ps1
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$basePath = Join-Path $here 'whale-base.png'
$icoPath = Join-Path $here 'app.ico'
$previewPath = Join-Path $here 'app-256.png'
if (-not (Test-Path -LiteralPath $basePath)) { throw "Missing $basePath" }

Add-Type -AssemblyName System.Drawing

$code = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class WhaleIconFactory
{
    public static void Build(string basePath, string icoPath, string previewPath)
    {
        using (Bitmap source = new Bitmap(basePath))
        using (Bitmap whale = Recolor(source))
        using (Bitmap master = AddBadge(whale))
        {
            SavePreview(master, previewPath, 256);
            int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
            List<byte[]> pngs = new List<byte[]>();
            foreach (int size in sizes)
            {
                using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    Bitmap sourceBitmap = master;
                    g.DrawImage(sourceBitmap, 0, 0, size, size);
                }
                using (MemoryStream stream = new MemoryStream())
                {
                    using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        Bitmap sourceBitmap = master;
                        g.DrawImage(sourceBitmap, 0, 0, size, size);
                    }
                }
                // The loop above is duplicated intentionally only for clarity in some runtimes;
                // create the PNG data in a separate helper below.
                pngs.Add(ToPng(master, size));
            }
            WriteIco(icoPath, sizes, pngs);
        }
    }

    private static Bitmap Recolor(Bitmap source)
    {
        Bitmap result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(result))
        {
            g.DrawImage(source, 0, 0, source.Width, source.Height);
        }
        int width = result.Width;
        int height = result.Height;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color c = result.GetPixel(x, y);
                if (c.A < 16) continue;
                int luminance = (c.R * 299 + c.G * 587 + c.B * 114) / 1000;
                if (luminance >= 195) continue;
                double strength = Math.Min(1.0, (195 - luminance) / 150.0);
                double t = (double)y / height;
                int blueR = (int)(76 + 12 * (1 - t));
                int blueG = (int)(108 + 20 * (1 - t));
                int blueB = (int)(250 - 18 * t);
                int r = (int)(c.R * (1 - strength) + blueR * strength);
                int g2 = (int)(c.G * (1 - strength) + blueG * strength);
                int b = (int)(c.B * (1 - strength) + blueB * strength);
                result.SetPixel(x, y, Color.FromArgb(c.A, r, g2, b));
            }
        }
        return result;
    }

    private static Bitmap AddBadge(Bitmap whale)
    {
        Bitmap result = new Bitmap(whale.Width, whale.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(result))
        {
            g.Clear(Color.Transparent);
            g.DrawImage(whale, 0, 0, whale.Width, whale.Height);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Original blue gear in the lower-right badge.
            int cx = 820;
            int cy = 810;
            int radius = 126;
            Rectangle badge = new Rectangle(cx - radius, cy - radius, radius * 2, radius * 2);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(242, 255, 255, 255)))
            {
                g.FillEllipse(bg, badge);
            }
            using (Pen border = new Pen(Color.FromArgb(205, 220, 255), 8f))
            {
                g.DrawEllipse(border, badge);
            }
            Color gearColor = Color.FromArgb(59, 91, 219);
            PointF[] teeth = new PointF[16];
            for (int i = 0; i < 16; i++)
            {
                double angle = Math.PI * 2 * i / 16 - Math.PI / 2;
                double r = (i % 2 == 0) ? 58 : 45;
                teeth[i] = new PointF((float)(cx + Math.Cos(angle) * r), (float)(cy + Math.Sin(angle) * r));
            }
            using (SolidBrush gear = new SolidBrush(gearColor))
            {
                g.FillPolygon(gear, teeth);
            }
            using (SolidBrush hole = new SolidBrush(Color.White))
            {
                g.FillEllipse(hole, cx - 18, cy - 18, 36, 36);
            }
        }
        return result;
    }

    private static byte[] ToPng(Bitmap source, int size)
    {
        using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, 0, 0, size, size);
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }
    }

    private static void SavePreview(Bitmap source, string path, int size)
    {
        using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawImage(source, 0, 0, size, size);
            bitmap.Save(path, ImageFormat.Png);
        }
    }

    private static void WriteIco(string path, int[] sizes, List<byte[]> pngs)
    {
        using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int size = sizes[i];
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)pngs[i].Length);
                writer.Write((uint)offset);
                offset += pngs[i].Length;
            }
            foreach (byte[] data in pngs)
            {
                writer.Write(data);
            }
        }
    }
}
'@

Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
[WhaleIconFactory]::Build($basePath, $icoPath, $previewPath)
"icon=$icoPath"
"preview=$previewPath"

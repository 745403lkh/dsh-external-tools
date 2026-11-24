using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DshExternalTools
{
    /// <summary>Line-style module icons drawn with GDI+ (no image assets needed).</summary>
    public static class ModuleIcon
    {
        public static void Draw(Graphics graphics, string id, Rectangle bounds, Color color, float stroke)
        {
            GraphicsState state = graphics.Save();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(bounds.Width / 48F, bounds.Height / 48F);
            using (Pen pen = new Pen(color, stroke))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                switch (id)
                {
                    case "plugin":
                        DrawPlugin(graphics, pen);
                        break;
                    case "backup":
                        DrawBackup(graphics, pen);
                        break;
                    case "diagnose":
                        DrawDiagnose(graphics, pen);
                        break;
                    case "log":
                        DrawLog(graphics, pen);
                        break;
                    case "safety":
                        DrawSafety(graphics, pen);
                        break;
                    case "repair":
                        DrawRepair(graphics, pen);
                        break;
                    case "process":
                        DrawProcess(graphics, pen);
                        break;
                    case "folder":
                        DrawFolder(graphics, pen);
                        break;
                    case "refresh":
                        DrawRefresh(graphics, pen);
                        break;
                    case "overview":
                        DrawOverview(graphics, pen);
                        break;
                    case "about":
                        DrawInfo(graphics, pen);
                        break;
                    case "web":
                        DrawWeb(graphics, pen);
                        break;
                    case "install":
                        DrawInstall(graphics, pen);
                        break;
                    default:
                        DrawInfo(graphics, pen);
                        break;
                }
            }
            graphics.Restore(state);
        }

        private static void DrawPlugin(Graphics g, Pen pen)
        {
            g.DrawRoundedRectangle(pen, 15, 18, 18, 15, 3);
            g.DrawLine(pen, 19, 11, 19, 18);
            g.DrawLine(pen, 29, 11, 29, 18);
            g.DrawLine(pen, 24, 33, 24, 38);
            g.DrawLine(pen, 19, 38, 24, 38);
        }

        private static void DrawBackup(Graphics g, Pen pen)
        {
            g.DrawRoundedRectangle(pen, 9, 16, 30, 23, 3);
            g.DrawLine(pen, 7, 12, 41, 12);
            g.DrawLine(pen, 7, 12, 7, 19);
            g.DrawLine(pen, 41, 12, 41, 19);
            g.DrawLine(pen, 19, 8, 29, 8);
            g.DrawLine(pen, 19, 8, 19, 12);
            g.DrawLine(pen, 29, 8, 29, 12);
            g.DrawLine(pen, 24, 21, 24, 32);
            g.DrawLine(pen, 19, 27, 24, 32);
            g.DrawLine(pen, 29, 27, 24, 32);
        }

        private static void DrawDiagnose(Graphics g, Pen pen)
        {
            g.DrawRoundedRectangle(pen, 7, 10, 34, 28, 4);
            g.DrawLine(pen, 11, 26, 16, 26);
            g.DrawLine(pen, 16, 26, 20, 18);
            g.DrawLine(pen, 20, 18, 26, 34);
            g.DrawLine(pen, 26, 34, 30, 24);
            g.DrawLine(pen, 30, 24, 37, 24);
        }

        private static void DrawLog(Graphics g, Pen pen)
        {
            g.DrawPath(pen, new GraphicsPath(new PointF[]
            {
                new PointF(12, 7), new PointF(30, 7), new PointF(37, 14),
                new PointF(37, 41), new PointF(12, 41), new PointF(12, 7)
            }, new byte[] { 0, 1, 1, 1, 1, 1 }));
            g.DrawLine(pen, 30, 7, 30, 14);
            g.DrawLine(pen, 30, 14, 37, 14);
            g.DrawLine(pen, 17, 21, 32, 21);
            g.DrawLine(pen, 17, 27, 32, 27);
            g.DrawLine(pen, 17, 33, 27, 33);
        }

        private static void DrawSafety(Graphics g, Pen pen)
        {
            GraphicsPath shield = new GraphicsPath();
            shield.AddLine(24, 6, 40, 12);
            shield.AddLine(40, 12, 40, 22);
            shield.AddBezier(40, 22, 40, 32, 33, 39, 24, 42);
            shield.AddBezier(24, 42, 15, 39, 8, 32, 8, 22);
            shield.AddLine(8, 22, 8, 12);
            shield.CloseFigure();
            g.DrawPath(pen, shield);
            g.DrawLine(pen, 16, 24, 22, 30);
            g.DrawLine(pen, 22, 30, 33, 18);
        }

        private static void DrawRepair(Graphics g, Pen pen)
        {
            // Grip, shaft and open jaw of a wrench.
            g.DrawLine(pen, 12, 36, 27, 21);
            g.DrawLine(pen, 17, 41, 32, 26);
            g.DrawArc(pen, 26, 10, 16, 16, 200, 230);
            g.DrawLine(pen, 29, 14, 34, 9);
            g.DrawLine(pen, 10, 34, 15, 39);
        }

        private static void DrawProcess(Graphics g, Pen pen)
        {
            g.DrawEllipse(pen, 8, 8, 32, 32);
            GraphicsPath triangle = new GraphicsPath();
            triangle.AddLine(20, 17, 33, 24);
            triangle.AddLine(33, 24, 20, 31);
            triangle.CloseFigure();
            g.DrawPath(pen, triangle);
        }

        private static void DrawFolder(Graphics g, Pen pen)
        {
            GraphicsPath folder = new GraphicsPath();
            folder.AddLine(7, 15, 7, 38);
            folder.AddLine(7, 38, 41, 38);
            folder.AddLine(41, 38, 41, 18);
            folder.AddLine(41, 18, 23, 18);
            folder.AddLine(23, 18, 19, 13);
            folder.AddLine(19, 13, 7, 13);
            folder.CloseFigure();
            g.DrawPath(pen, folder);
        }

        private static void DrawRefresh(Graphics g, Pen pen)
        {
            g.DrawArc(pen, 9, 9, 30, 30, 40, 280);
            g.DrawLine(pen, 33, 8, 39, 14);
            g.DrawLine(pen, 39, 14, 33, 19);
        }

        private static void DrawOverview(Graphics g, Pen pen)
        {
            g.DrawRoundedRectangle(pen, 8, 8, 13, 13, 2);
            g.DrawRoundedRectangle(pen, 27, 8, 13, 13, 2);
            g.DrawRoundedRectangle(pen, 8, 27, 13, 13, 2);
            g.DrawRoundedRectangle(pen, 27, 27, 13, 13, 2);
        }

        private static void DrawInfo(Graphics g, Pen pen)
        {
            g.DrawEllipse(pen, 8, 8, 32, 32);
            g.DrawLine(pen, 24, 22, 24, 33);
            g.DrawLine(pen, 24, 15, 24, 16);
        }

        private static void DrawWeb(Graphics g, Pen pen)
        {
            g.DrawRoundedRectangle(pen, 7, 9, 34, 24, 4);
            g.DrawLine(pen, 24, 33, 24, 38);
            g.DrawLine(pen, 17, 39, 31, 39);
            g.DrawEllipse(pen, 17, 15, 14, 12);
            g.DrawLine(pen, 17, 21, 31, 21);
            g.DrawLine(pen, 24, 15, 24, 27);
        }

        private static void DrawInstall(Graphics g, Pen pen)
        {
            g.DrawRoundedRectangle(pen, 9, 16, 30, 24, 3);
            g.DrawLine(pen, 7, 12, 41, 12);
            g.DrawLine(pen, 7, 12, 7, 19);
            g.DrawLine(pen, 41, 12, 41, 19);
            g.DrawLine(pen, 24, 19, 24, 32);
            g.DrawLine(pen, 19, 27, 24, 32);
            g.DrawLine(pen, 29, 27, 24, 32);
        }
    }

    internal static class GraphicsExtensions
    {
        public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, int left, int top, int width, int height, int radius)
        {
            using (GraphicsPath path = Ui.Rounded(new Rectangle(left, top, width, height), radius))
            {
                graphics.DrawPath(pen, path);
            }
        }
    }
}

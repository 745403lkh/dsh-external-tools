using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DshExternalTools.Modules
{
    /// <summary>Portable app information, paths and license.</summary>
    public class AboutModule : ToolModule
    {
        public override string Id { get { return "about"; } }
        public override string Title { get { return "关于"; } }
        public override string Description { get { return "便携版说明、路径与许可。"; } }
        public override string Category { get { return "信息"; } }
        public override string IconId { get { return "about"; } }
        public override Color Accent { get { return Ui.Accent; } }

        private TextBox text;

        public override Panel CreateView(MainForm host)
        {
            Panel root = new Panel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Ui.Window;
            root.AutoScroll = true;

            Panel hero = Ui.Card(root, 0, 0, 900, 104);
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null)
                {
                    PictureBox logo = new PictureBox();
                    logo.Left = 18;
                    logo.Top = 22;
                    logo.Width = 52;
                    logo.Height = 52;
                    logo.SizeMode = PictureBoxSizeMode.Zoom;
                    logo.Image = icon.ToBitmap();
                    hero.Controls.Add(logo);
                }
            }
            catch { }
            Ui.Label(hero, "DSH 外部工具", 86, 10, 400, 36, 18F, FontStyle.Bold, Ui.Ink, false);
            Ui.Label(hero, Program.DisplayVersion + (L.IsEnglish ? " · Portable, single EXE, ready to run" : " · 便携版，单个 EXE，点开即用"), 88, 50, 600, 24, 9F, FontStyle.Regular, Ui.Muted, false);
            Ui.Label(hero, L.IsEnglish ? "No DSH / Node / Electron runtime; standalone, no DSH plugin required." : "不加载 DSH / Node / Electron；独立运行，不依赖 DSH 插件。", 88, 76, 760, 22, 9F, FontStyle.Regular, Ui.SecondaryInk, false);

            Panel body = Ui.Card(root, 0, 116, 900, 360);
            body.Padding = new Padding(16);
            text = Ui.ReadOnlyText(body, false);
            text.Dock = DockStyle.Fill;
            text.BorderStyle = BorderStyle.None;
            text.BackColor = Ui.CardFill;

            root.Resize += delegate
            {
                int width = Math.Max(620, root.ClientSize.Width - 4);
                hero.Width = width;
                body.Width = width;
                body.Height = Math.Max(260, root.ClientSize.Height - 124);
            };
            return root;
        }

        public override void Refresh(MainForm host)
        {
            if (text == null) return;
            text.Text = string.Join("\r\n", L.IsEnglish
                ? new string[]
                {
                    "Portable: a single EXE with no installer, service, or background process.",
                    "It does not load DSH, Node, or Electron; it only reads/writes profile files and starts/stops the local DSH process.",
                    "Requires Windows .NET Framework 4.x (included with Windows 10/11).",
                    "",
                    "Version: " + Program.DisplayVersion,
                    "DSH: " + (DshEnvironment.InstalledVersion ?? "not found") + " (verified: " + DshEnvironment.SupportedLabel + ")",
                    "EXE: " + Application.ExecutablePath,
                    "profile: " + (host.Store == null ? "not found" : host.Store.ProfileDir),
                    "manifest: " + (host.Store == null ? "not found" : host.Store.ManifestPath),
                    "DSH executable: " + (ManifestStore.FindDshExecutable() ?? "not found"),
                    "",
                    "Protected plugins (cannot be disabled):",
                    "  " + string.Join(", ", ManifestStore.ProtectedNames),
                    "",
                    "License: MIT. Log analysis is offline rule matching; no data is uploaded."
                }
                : new string[]
                {
                    "便携版：这是一个单独的 EXE，不需要安装器、服务或后台进程。",
                    "运行时不加载 DSH、Node 或 Electron；只读写 profile 文件、启动/结束本机 DSH 进程。",
                    "需要 Windows .NET Framework 4.x（Windows 10/11 自带）。",
                    "",
                    "版本: " + Program.DisplayVersion,
                    "DSH: " + (DshEnvironment.InstalledVersion ?? "未找到") + "（本工具验证 " + DshEnvironment.SupportedLabel + "）",
                    "EXE: " + Application.ExecutablePath,
                    "profile: " + (host.Store == null ? "未找到" : host.Store.ProfileDir),
                    "manifest: " + (host.Store == null ? "未找到" : host.Store.ManifestPath),
                    "DSH 可执行文件: " + (ManifestStore.FindDshExecutable() ?? "未找到"),
                    "",
                    "受保护插件（不能禁用）:",
                    "  " + string.Join(", ", ManifestStore.ProtectedNames),
                    "",
                    "许可: MIT。日志分析是离线规则匹配，不会上传任何数据。"
                });
        }
    }
}

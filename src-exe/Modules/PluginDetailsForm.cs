using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace DshExternalTools.Modules
{
    /// <summary>Read-only package details for the selected bundle.</summary>
    public class PluginDetailsForm : Form
    {
        private readonly BundleInfo bundle;

        public PluginDetailsForm(BundleInfo value)
        {
            bundle = value;
            Text = bundle.DisplayName + " · " + bundle.Name;
            Width = 820;
            Height = 620;
            MinimumSize = new Size(680, 460);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Ui.Window;
            Font = new Font(Ui.FontFamily, 9F);
            ShowInTaskbar = false;

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 94;
            header.BackColor = Ui.SurfaceFill;
            header.Padding = new Padding(24, 14, 24, 8);
            Controls.Add(header);

            Ui.Label(header, "插件详情", 0, 2, 360, 36, 18F, FontStyle.Bold, Ui.Ink, false);
            Ui.Label(header, bundle.DisplayName + "  ·  " + bundle.Name, 0, 42, 700, 24,
                9.5F, FontStyle.Regular, Ui.SecondaryInk, false);

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.Padding = new Padding(24, 14, 24, 8);
            body.BackColor = Ui.Window;
            Controls.Add(body);

            TextBox text = Ui.ReadOnlyText(body, false);
            text.Dock = DockStyle.Fill;
            text.BackColor = Ui.SurfaceFill;
            text.Font = new Font(Ui.FontFamily, 9.5F);
            text.Text = BuildDetails();

            Panel actions = new Panel();
            actions.Dock = DockStyle.Bottom;
            actions.Height = 62;
            actions.BackColor = Ui.Window;
            actions.Padding = new Padding(24, 10, 24, 12);
            Controls.Add(actions);

            Button open = Ui.Button(actions, "打开插件目录", 0, 8, 126, 34, ButtonKind.Secondary,
                delegate { OpenFolder(); });
            open.Enabled = !string.IsNullOrEmpty(bundle.PackagePath) && Directory.Exists(bundle.PackagePath);
            Ui.Button(actions, "复制包名", 138, 8, 104, 34, ButtonKind.Secondary,
                delegate { CopyName(); });
            Ui.Button(actions, "关闭", 254, 8, 86, 34, ButtonKind.Secondary,
                delegate { Close(); });

            Ui.Label(actions, "npm 更新会修改 profile 的 package.json，更新前 EXE 会先备份。",
                354, 12, 400, 24, 8.5F, FontStyle.Regular, Ui.Muted, false);
        }

        private string BuildDetails()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("名称      " + bundle.DisplayName);
            builder.AppendLine("包名      " + bundle.Name);
            builder.AppendLine("版本      " + (string.IsNullOrEmpty(bundle.Version) ? "—" : bundle.Version));
            builder.AppendLine("状态      " + (bundle.Enabled ? "已启用" : "已禁用"));
            builder.AppendLine("类型      " + KindText());
            builder.AppendLine("安装路径  " + (string.IsNullOrEmpty(bundle.PackagePath) ? "未安装或随 DSH 内置" : bundle.PackagePath));
            builder.AppendLine("依赖声明  " + (string.IsNullOrEmpty(bundle.DependencySpec) ? "—" : bundle.DependencySpec));
            if (!string.IsNullOrEmpty(bundle.Author)) builder.AppendLine("作者      " + bundle.Author);
            if (!string.IsNullOrEmpty(bundle.License)) builder.AppendLine("许可      " + bundle.License);
            if (!string.IsNullOrEmpty(bundle.Repository)) builder.AppendLine("仓库      " + bundle.Repository);
            builder.AppendLine();
            builder.AppendLine("说明");
            builder.AppendLine(string.IsNullOrEmpty(bundle.Description) ? "—" : bundle.Description);
            builder.AppendLine();

            string readme = ReadReadme();
            if (readme != "")
            {
                builder.AppendLine("README 摘要");
                builder.AppendLine(readme);
            }
            return builder.ToString();
        }

        private string KindText()
        {
            if (bundle.Builtin) return "DSH 内置";
            if (!string.IsNullOrEmpty(bundle.DependencySpec) &&
                (bundle.DependencySpec.StartsWith("link:", StringComparison.OrdinalIgnoreCase) ||
                 bundle.DependencySpec.StartsWith("file:", StringComparison.OrdinalIgnoreCase)))
            {
                return "本地链接";
            }
            return bundle.Official ? "官方 npm" : "第三方 npm";
        }

        private string ReadReadme()
        {
            if (string.IsNullOrEmpty(bundle.PackagePath)) return "";
            string[] names = new string[] { "README.md", "README_ZH.md", "README.zh.md", "readme.md" };
            foreach (string name in names)
            {
                string path = Path.Combine(bundle.PackagePath, name);
                if (!File.Exists(path)) continue;
                try
                {
                    string value = File.ReadAllText(path, Encoding.UTF8).Trim();
                    if (value.Length > 7000) value = value.Substring(0, 7000) + "\r\n…";
                    return value;
                }
                catch
                {
                    return "";
                }
            }
            return "";
        }

        private void OpenFolder()
        {
            if (string.IsNullOrEmpty(bundle.PackagePath) || !Directory.Exists(bundle.PackagePath)) return;
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", "\"" + bundle.PackagePath + "\"");
            }
            catch
            {
            }
        }

        private void CopyName()
        {
            try
            {
                Clipboard.SetText(bundle.Name);
            }
            catch
            {
            }
        }
    }
}

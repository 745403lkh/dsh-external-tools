using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DshExternalTools.Modules
{
    /// <summary>
    /// Opens the official installer. It does not uninstall silently; opening the
    /// package is the whole reinstall path.
    /// </summary>
    public class ReinstallModule : ToolModule
    {
        public override string Id { get { return "reinstall"; } }
        public override string Title { get { return "重装 DSH"; } }
        public override string Description { get { return "打开安装包覆盖安装，不自动卸载。"; } }
        public override string Category { get { return "维护"; } }
        public override string IconId { get { return "install"; } }
        public override Color Accent { get { return Ui.Accent; } }

        private Label version;
        private Label state;
        private Label note;
        private Button openButton;
        private Label installerPath;
        private Panel root;

        public override Panel CreateView(MainForm host)
        {
            root = new Panel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Ui.Window;
            root.AutoScroll = true;

            Panel current = Ui.Card(root, 0, 0, 900, 154);
            Ui.Title(current, "当前安装", 18, 14, 300, 24);
            version = Ui.Label(current, "—", 18, 44, 780, 24, 10F, FontStyle.Regular, Ui.SecondaryInk, true);
            state = Ui.Label(current, "—", 18, 72, 700, 24, 10F, FontStyle.Bold, Ui.Muted, false);
            note = Ui.Label(current, "", 18, 100, 820, 40, 9F, FontStyle.Regular, Ui.Muted, true);
            note.AutoSize = false;

            Panel action = Ui.Card(root, 0, 172, 900, 190);
            Ui.Title(action, "打开安装包", 18, 14, 300, 24);
            Ui.Hint(action, L.IsEnglish
                ? "Does not uninstall or install silently; opens the official installer to overwrite the current version."
                : "不自动卸载，也不静默安装；只启动官方安装器，按提示覆盖安装。安装器可能要求管理员权限。", 18, 42, 800, 38);
            openButton = Ui.Button(action, "选择并打开安装包…", 18, 90, 190, 34, ButtonKind.Primary, delegate { Open(host); });
            Ui.Button(action, "重新检测版本", 220, 90, 130, 34, ButtonKind.Secondary, delegate
            {
                DshEnvironment.Refresh();
                host.RefreshState();
                Refresh(host);
            });
            Ui.Button(action, "打开安装目录", 362, 90, 160, 34, ButtonKind.Secondary, delegate
            {
                if (!string.IsNullOrEmpty(DshEnvironment.Executable)) host.OpenFolder(Path.GetDirectoryName(DshEnvironment.Executable));
            });
            installerPath = Ui.Label(action, "自动查找: —", 18, 130, 820, 20, 8.5F, FontStyle.Regular, Ui.Muted, true);
            Ui.Hint(action, "版本不匹配时，本模块会禁用打开入口，避免装错版本。", 18, 156, 760, 20);

            root.Resize += delegate
            {
                int width = Math.Max(620, root.ClientSize.Width - 4);
                current.Width = width;
                action.Width = width;
                if (note != null) note.Width = Math.Max(300, width - 40);
            };
            return root;
        }

        public override void Refresh(MainForm host)
        {
            DshEnvironment.Refresh();
            version.Text = L.IsEnglish
                ? "Installed: " + (DshEnvironment.InstalledVersion ?? "not found") +
                  "    Verified: " + DshEnvironment.SupportedLabel
                : "已安装: " + (DshEnvironment.InstalledVersion ?? "未找到") +
                  "    本工具验证: " + DshEnvironment.SupportedLabel;
            state.Text = L.T(DshEnvironment.StateText);
            if (DshEnvironment.State == DshVersionState.Matched)
            {
                state.ForeColor = Ui.Success;
                note.Text = L.T("版本匹配，可以打开安装包覆盖安装。个人 profile 不会被删除。");
                openButton.Enabled = true;
            }
            else if (DshEnvironment.State == DshVersionState.Missing)
            {
                state.ForeColor = Ui.Warn;
                note.Text = L.T("未检测到可验证的 DSH 安装；仍可以打开安装包进行安装。");
                openButton.Enabled = true;
            }
            else
            {
                state.ForeColor = Ui.Danger;
                note.Text = L.T("当前 DSH 版本不在本工具验证范围内，重装入口已禁用。请使用匹配版本的安装包，或更新本工具。");
                openButton.Enabled = false;
            }
            string found = DshEnvironment.FindInstaller();
            installerPath.Text = found == null
                ? L.T("自动查找: 未找到，请点击按钮选择安装包")
                : (L.IsEnglish ? "Auto-found: " : "自动查找: ") + found;
            openButton.Text = found == null ? L.T("选择安装包…") : L.T("打开安装包");
        }

        private void Open(MainForm host)
        {
            DshEnvironment.Refresh();
            if (!DshEnvironment.ReinstallAllowed)
            {
                MessageBox.Show(host,
                    L.IsEnglish
                        ? "Current DSH version is " + (DshEnvironment.InstalledVersion ?? "unknown") +
                          ", while this tool verifies " + DshEnvironment.SupportedLabel + ".\n\nReinstall is disabled to avoid installing the wrong version."
                        : "当前 DSH 版本为 " + (DshEnvironment.InstalledVersion ?? "未知") +
                          "，本工具按 " + DshEnvironment.SupportedLabel + " 验证。\n\n为避免装错版本，重装入口已禁用。",
                    L.T("版本不匹配"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string installer = DshEnvironment.FindInstaller();
            if (string.IsNullOrEmpty(installer))
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = L.T("选择 DeepSeek Harness 安装包");
                    dialog.Filter = L.IsEnglish
                        ? "Installer (*.exe)|*.exe|All files (*.*)|*.*"
                        : "安装包 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                    dialog.CheckFileExists = true;
                    if (dialog.ShowDialog(host) != DialogResult.OK) return;
                    installer = dialog.FileName;
                }
            }

            if (!DshEnvironment.LooksLikeInstaller(installer))
            {
                MessageBox.Show(host, L.T("这个文件看起来不是 DSH 安装包（不能是主程序或卸载器）。"),
                    L.T("无效安装包"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (ManifestStore.DshProcessCount() > 0)
            {
                if (MessageBox.Show(host,
                    L.T("DSH 正在运行。安装器通常要求先退出 DSH。\n\n仍要打开安装包吗？"),
                    L.T("DSH 正在运行"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    return;
                }
            }

            if (MessageBox.Show(host,
                L.IsEnglish
                    ? "Opening installer:\n" + installer + "\n\nIt will not uninstall automatically; the installer may request administrator rights. Continue?"
                    : "将打开安装包：\n" + installer + "\n\n不会自动卸载；安装器可能要求管理员权限。继续？",
                L.T("打开安装包"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                DshEnvironment.OpenInstaller(installer);
            }
            catch (Exception error)
            {
                MessageBox.Show(host, error.Message, L.T("打开失败"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

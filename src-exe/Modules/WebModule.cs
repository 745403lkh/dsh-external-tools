using System;
using System.Drawing;
using System.Windows.Forms;

namespace DshExternalTools.Modules
{
    /// <summary>Starts the separate web profile, isolated from the desktop profile.</summary>
    public class WebModule : ToolModule
    {
        public override string Id { get { return "web"; } }
        public override string Title { get { return "Web 端 DSH"; } }
        public override string Description { get { return "启动独立 web profile，用浏览器打开。"; } }
        public override string Category { get { return "诊断"; } }
        public override string IconId { get { return "web"; } }
        public override Color Accent { get { return Ui.Accent; } }

        private Label version;
        private Label state;
        private Button start;
        private Panel root;

        public override Panel CreateView(MainForm host)
        {
            root = new Panel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Ui.Window;
            root.AutoScroll = true;

            Panel info = Ui.Card(root, 0, 0, 900, 176);
            Ui.Title(info, "Web profile（隔离）", 18, 14, 400, 24);
            Ui.Hint(info, L.IsEnglish
                ? "Web uses %DSH_HOME%\\profiles\\web, isolated from desktop plugins and settings. Use it to open a working session and repair the desktop profile."
                : "Web 端使用 %DSH_HOME%\\profiles\\web，与 desktop 的插件、设置、会话隔离。可用它启动独立会话，借助文件/终端工具修 desktop profile；两边插件/设置不共享。",
                18, 42, 780, 54);
            version = Ui.Label(info, "—", 18, 104, 700, 22, 9F, FontStyle.Regular, Ui.SecondaryInk, true);
            ToolTip cliTip = new ToolTip();
            cliTip.SetToolTip(version, DshEnvironment.CliBinPath ?? "");
            state = Ui.Label(info, "—", 18, 128, 700, 22, 9F, FontStyle.Bold, Ui.Muted, false);

            Panel how = Ui.Card(root, 0, 192, 900, 176);
            Ui.Title(how, "启动", 18, 14, 300, 24);
            Ui.Hint(how, L.IsEnglish
                ? "Opens a terminal using the absolute DSH CLI path; no PATH or DSH_* variables are required. The CLI prints a URL and opens the browser. Close the terminal to stop."
                : "会打开一个终端，用 DSH 自带 CLI 的绝对路径启动 web profile，不依赖 PATH 或 DSH_* 环境变量；CLI 会打印 URL 并自动打开浏览器。关闭终端或结束进程即停止。",
                18, 42, 780, 54);
            start = Ui.Button(how, "启动 Web 端 DSH", 18, 104, 170, 34, ButtonKind.Primary, delegate { Start(host); });
            Ui.Button(how, "复制 cmd 命令", 198, 104, 130, 34, ButtonKind.Secondary, delegate
            {
                host.CopyText(DshEnvironment.WebCommandDisplay, "Web 启动 cmd 命令");
            });
            Ui.Hint(how, L.IsEnglish
                ? "Port 0 is assigned by the system to avoid conflicts with desktop port 1666; see the terminal for the URL."
                : "端口 0 由系统分配，避免和桌面端 1666 冲突；URL 看终端输出。", 18, 146, 760, 20);

            root.Resize += delegate
            {
                int width = Math.Max(620, root.ClientSize.Width - 4);
                info.Width = width;
                how.Width = width;
            };
            return root;
        }

        public override void Refresh(MainForm host)
        {
            DshEnvironment.Refresh();
            version.Text = L.IsEnglish
                ? "Current DSH: " + (DshEnvironment.InstalledVersion ?? "not found") +
                  "    CLI: " + (DshEnvironment.CliBinPath == null ? "not found" : "found")
                : "当前 DSH: " + (DshEnvironment.InstalledVersion ?? "未找到") +
                  "    CLI: " + (DshEnvironment.CliBinPath == null ? "未找到" : "已找到");
            if (DshEnvironment.State == DshVersionState.Matched)
            {
                state.Text = L.T("版本匹配，可以启动");
                state.ForeColor = Ui.Success;
            }
            else if (DshEnvironment.State == DshVersionState.Missing)
            {
                state.Text = L.T("未找到 DSH，无法启动");
                state.ForeColor = Ui.Danger;
            }
            else
            {
                state.Text = L.T("版本不匹配；仍可尝试启动，Web profile 可能受 CLI 变化影响");
                state.ForeColor = Ui.Warn;
            }
            start.Enabled = DshEnvironment.WebAvailable;
        }

        private void Start(MainForm host)
        {
            if (!DshEnvironment.WebAvailable)
            {
                MessageBox.Show(host, L.T("未找到 DSH 可执行文件或 CLI 路径，无法启动 Web 端。"),
                    L.T("Web 端不可用"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show(host,
                L.IsEnglish
                    ? "Start the isolated web profile?\n\nA terminal will open and the browser will open automatically; the desktop profile is not modified."
                    : "启动独立的 web profile？\n\n会打开终端并自动打开浏览器；不会修改 desktop profile。",
                L.T("启动 Web 端 DSH"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
            try
            {
                DshEnvironment.StartWeb();
                MessageBox.Show(host,
                    L.IsEnglish
                        ? "dsh web has started in a terminal.\n\nThe browser should open automatically; if not, use the URL printed in the terminal.\nClose the terminal or end the process to stop it."
                        : "已在终端启动 dsh web。\n\n浏览器会自动打开；如果没有，请查看终端打印的 URL。\n关闭终端或结束该进程即可停止。",
                    L.T("已启动"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error)
            {
                MessageBox.Show(host, error.Message, L.T("启动失败"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

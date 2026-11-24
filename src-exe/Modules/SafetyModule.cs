using System;
using System.Drawing;
using System.Windows.Forms;

namespace DshExternalTools.Modules
{
    /// <summary>Safe mode and patch-layer recovery.</summary>
    public class SafetyModule : ToolModule
    {
        public override string Id { get { return "safety"; } }
        public override string Title { get { return "安全模式"; } }
        public override string Description { get { return "只保留核心 bundle，快速定位问题。"; } }
        public override string Category { get { return "急救"; } }
        public override string IconId { get { return "safety"; } }
        public override Color Accent { get { return Ui.Accent; } }

        private Label status;
        private Button toggle;
        private Button start;
        private Panel root;

        public override Panel CreateView(MainForm host)
        {
            root = new Panel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Ui.Window;
            root.AutoScroll = true;

            Panel safe = Ui.Card(root, 0, 0, 900, 154);
            Ui.Title(safe, "安全模式", 18, 14, 300, 24);
            Ui.Hint(safe, "只保留 dsh-base / dsh-web-app，其余 bundle 临时移出启动列表；写入前自动备份。", 18, 42, 700, 38);
            status = Ui.Label(safe, "—", 18, 84, 400, 24, 10F, FontStyle.Bold, Ui.Ink, false);
            toggle = Ui.Button(safe, "进入安全模式", 18, 110, 150, 34, ButtonKind.Primary, delegate { host.ToggleSafeMode(false); });
            start = Ui.Button(safe, "安全模式并启动", 178, 110, 160, 34, ButtonKind.Secondary, delegate { host.ToggleSafeMode(true); });

            Panel patch = Ui.Card(root, 0, 170, 900, 140);
            Ui.Title(patch, "配置文件", 18, 14, 300, 24);
            Ui.Hint(patch, "cordis.patch.yml 损坏时，先备份再重置为 []。不会删除任何 bundle。", 18, 42, 700, 38);
            Ui.Button(patch, "重置 cordis.patch.yml", 18, 86, 180, 34, ButtonKind.Danger, delegate { host.ResetPatch(); });
            Ui.Button(patch, "打开配置目录", 210, 86, 130, 34, ButtonKind.Secondary, delegate { if (host.Store != null) host.OpenFolder(host.Store.ProfileDir); });
            Ui.Button(patch, "打开日志目录", 350, 86, 130, 34, ButtonKind.Secondary, delegate { host.OpenLogFolder(null); });

            Panel process = Ui.Card(root, 0, 326, 900, 104);
            Ui.Title(process, "进程", 18, 14, 300, 24);
            Ui.Hint(process, "DSH 卡死或端口占用时，结束所有 DeepSeek Harness 进程后重开。", 18, 42, 700, 22);
            Ui.Button(process, "结束 DSH 进程", 18, 64, 150, 34, ButtonKind.Danger, delegate { host.KillDsh(); });
            Ui.Button(process, "启动 DSH", 178, 64, 120, 34, ButtonKind.Secondary, delegate { host.StartDsh(); });

            root.Resize += delegate
            {
                int width = Math.Max(620, root.ClientSize.Width - 4);
                safe.Width = width;
                patch.Width = width;
                process.Width = width;
            };
            return root;
        }

        public override void Refresh(MainForm host)
        {
            if (host.Snapshot == null)
            {
                status.Text = L.T("未找到 profile");
                status.ForeColor = Ui.Danger;
                toggle.Enabled = false;
                start.Enabled = false;
                return;
            }
            if (host.Snapshot.SafeMode)
            {
                status.Text = L.T("安全模式已开启");
                status.ForeColor = Ui.Warn;
                toggle.Text = L.T("退出安全模式");
                start.Text = L.T("退出并启动");
            }
            else
            {
                status.Text = L.T("正常");
                status.ForeColor = Ui.Success;
                toggle.Text = L.T("进入安全模式");
                start.Text = L.T("安全模式并启动");
            }
            toggle.Enabled = true;
            start.Enabled = true;
        }
    }
}

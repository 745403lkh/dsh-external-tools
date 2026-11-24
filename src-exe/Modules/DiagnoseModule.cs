using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace DshExternalTools.Modules
{
    /// <summary>Profile checks plus the rule-based log analysis report.</summary>
    public class DiagnoseModule : ToolModule
    {
        public override string Id { get { return "diagnose"; } }
        public override string Title { get { return "一键诊断"; } }
        public override string Description { get { return "检查配置、依赖、日志和进程。"; } }
        public override string Category { get { return "诊断"; } }
        public override string IconId { get { return "diagnose"; } }
        public override Color Accent { get { return Ui.Accent; } }

        private TextBox reportBox;
        private string report = "";

        public override Panel CreateView(MainForm host)
        {
            Panel root = new Panel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Ui.Window;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.BackColor = Ui.Window;
            layout.Padding = new Padding(0);
            layout.Margin = new Padding(0);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(layout);

            Panel toolbar = new Panel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.BackColor = Ui.Window;
            Ui.Label(toolbar, "诊断报告", 0, 12, 220, 24, 12F, FontStyle.Bold, Ui.Ink, false);
            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Right;
            actions.Width = 360;
            actions.BackColor = Ui.Window;
            actions.WrapContents = false;
            Ui.Button(actions, "重新诊断", 0, 0, 96, 32, ButtonKind.Primary, delegate { Run(host); });
            Ui.Button(actions, "复制报告", 0, 0, 96, 32, ButtonKind.Secondary, delegate { host.CopyText(report, "诊断报告"); });
            Ui.Button(actions, "导出诊断包", 0, 0, 116, 32, ButtonKind.Secondary, delegate { host.ExportDiagnostics(report); });
            toolbar.Controls.Add(actions);
            layout.Controls.Add(toolbar, 0, 0);

            Panel card = Ui.Surface();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(14);
            layout.Controls.Add(card, 0, 1);
            reportBox = Ui.ReadOnlyText(card, false);
            reportBox.Dock = DockStyle.Fill;
            return root;
        }

        public override void Refresh(MainForm host)
        {
            if (reportBox != null && reportBox.Text.Length == 0)
            {
                Run(host);
            }
        }

        private void Run(MainForm host)
        {
            if (host.Store == null)
            {
                report = L.T("还没有找到 profile。请在“修复工具”中选择包含 package.json 的目录。");
                if (reportBox != null) reportBox.Text = report;
                return;
            }
            try
            {
                List<DiagItem> items = host.Store.Diagnose();
                report = Format(host, items);
                if (reportBox != null)
                {
                    reportBox.Text = report;
                    reportBox.SelectionStart = 0;
                    reportBox.ScrollToCaret();
                }
            }
            catch (Exception error)
            {
                report = (L.IsEnglish ? "Diagnosis failed:\r\n" : "诊断失败：\r\n") + error.Message;
                if (reportBox != null) reportBox.Text = report;
            }
        }

        private static string Format(MainForm host, List<DiagItem> items)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("DSH 外部工具 " + Program.DisplayVersion);
            builder.AppendLine(L.T("时间: ") + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine("profile: " + (host.Store == null ? "" : host.Store.ProfileDir));
            builder.AppendLine("manifest: " + (host.Store == null ? "" : host.Store.ManifestPath));
            builder.AppendLine(L.T("DSH 进程: ") + ManifestStore.DshProcessCount());
            builder.AppendLine(L.T("DSH 可执行文件: ") + (ManifestStore.FindDshExecutable() ?? L.T("未找到")));
            builder.AppendLine(L.T("DSH 版本: ") + (DshEnvironment.InstalledVersion ?? L.T("未找到")) +
                L.T("（本工具验证 ") + DshEnvironment.SupportedLabel + " / " + L.T(DshEnvironment.StateText) + L.T("）"));
            builder.AppendLine();
            int errors = 0;
            int warnings = 0;
            foreach (DiagItem item in items)
            {
                builder.AppendLine("[" + item.Level + "] " + L.T(item.Title) + ": " + item.Detail);
                if (item.Level == "ERROR") errors++;
                else if (item.Level == "WARN") warnings++;
            }
            builder.AppendLine();
            builder.AppendLine(L.IsEnglish
                ? "Summary: " + errors + " error(s), " + warnings + " warning(s)"
                : "汇总: " + errors + " 个错误, " + warnings + " 个警告");
            if (errors > 0)
            {
                builder.AppendLine(L.T("建议: 先处理 ERROR；若是插件导致 DSH 起不来，进入“安全模式”，再逐个启用定位。"));
            }
            else if (warnings > 0)
            {
                builder.AppendLine(L.T("建议: 检查 WARN；必要时恢复备份或重置 patch 层。"));
            }
            else
            {
                builder.AppendLine(L.T("建议: 未发现阻断问题。"));
            }
            return builder.ToString();
        }
    }
}

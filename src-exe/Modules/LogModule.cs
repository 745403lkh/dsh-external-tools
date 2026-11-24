using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DshExternalTools.Modules
{
    /// <summary>Log browser plus the offline rule-based error analysis.</summary>
    public class LogModule : ToolModule
    {
        public override string Id { get { return "logs"; } }
        public override string Title { get { return "日志分析"; } }
        public override string Description { get { return "查看日志，自动分析常见错误。"; } }
        public override string Category { get { return "诊断"; } }
        public override string IconId { get { return "log"; } }
        public override Color Accent { get { return Ui.Accent; } }

        private ListView logList;
        private TextBox logBox;
        private ListView findingList;

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
            Ui.Label(toolbar, "日志与错误", 0, 12, 220, 24, 12F, FontStyle.Bold, Ui.Ink, false);
            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Right;
            actions.Width = 420;
            actions.BackColor = Ui.Window;
            actions.WrapContents = false;
            Ui.Button(actions, "刷新", 0, 0, 78, 32, ButtonKind.Secondary, delegate { Refresh(host); });
            Ui.Button(actions, "打开目录", 0, 0, 96, 32, ButtonKind.Secondary, delegate { OpenFolder(host); });
            Ui.Button(actions, "复制日志", 0, 0, 96, 32, ButtonKind.Secondary, delegate { host.CopyText(logBox.Text, "当前日志"); });
            Ui.Button(actions, "智能分析", 0, 0, 96, 32, ButtonKind.Primary, delegate { Analyze(host); });
            toolbar.Controls.Add(actions);
            layout.Controls.Add(toolbar, 0, 0);

            Panel card = Ui.Surface();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(12);
            layout.Controls.Add(card, 0, 1);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.Panel1.BackColor = Ui.SurfaceFill;
            split.Panel2.BackColor = Ui.SurfaceFill;
            split.SizeChanged += delegate
            {
                if (split.Width <= 700) return;
                try
                {
                    split.Panel1MinSize = 240;
                    split.Panel2MinSize = 360;
                    int target = (int)(split.Width * 0.36);
                    if (target < 240) target = 240;
                    if (target > split.Width - 360) target = split.Width - 360;
                    split.SplitterDistance = target;
                }
                catch { }
            };
            card.Controls.Add(split);

            logList = Ui.List(split.Panel1);
            Ui.Column(logList, "日志文件", 190);
            Ui.Column(logList, "大小", 60);
            Ui.Column(logList, "时间", 130);
            logList.SelectedIndexChanged += delegate { LoadSelected(); };

            TableLayoutPanel right = new TableLayoutPanel();
            right.Dock = DockStyle.Fill;
            right.ColumnCount = 1;
            right.RowCount = 2;
            right.BackColor = Ui.SurfaceFill;
            right.Padding = new Padding(12, 0, 0, 0);
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 168F));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            split.Panel2.Controls.Add(right);

            findingList = Ui.List(right);
            Ui.Column(findingList, "级别", 58);
            Ui.Column(findingList, "问题", 150);
            Ui.Column(findingList, "次数", 48);
            Ui.Column(findingList, "建议", 260);
            findingList.DoubleClick += delegate
            {
                if (findingList.SelectedItems.Count == 0) return;
                LogFinding finding = findingList.SelectedItems[0].Tag as LogFinding;
                if (finding == null) return;
                MessageBox.Show(host, L.T(finding.Suggestion) + (L.IsEnglish ? "\r\n\r\nSample:\r\n" : "\r\n\r\n样本：\r\n") + finding.Sample,
                    L.T(finding.Title), MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            logBox = Ui.ReadOnlyText(right, true);
            logBox.Dock = DockStyle.Fill;

            right.Controls.Add(findingList, 0, 0);
            right.Controls.Add(logBox, 0, 1);
            return root;
        }

        public override void Refresh(MainForm host)
        {
            if (host.Store == null || logList == null) return;
            logList.BeginUpdate();
            logList.Items.Clear();
            List<string> files = ManifestStore.FindLogFiles(host.Store.ProfileDir);
            foreach (string file in files)
            {
                FileInfo info = new FileInfo(file);
                ListViewItem item = new ListViewItem(info.Name);
                item.SubItems.Add(FormatBytes(info.Length));
                item.SubItems.Add(info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                item.Tag = file;
                logList.Items.Add(item);
            }
            logList.EndUpdate();
            if (logList.Items.Count > 0 && logList.SelectedItems.Count == 0)
            {
                logList.Items[0].Selected = true;
                LoadSelected();
            }
            else if (logList.Items.Count == 0 && logBox != null)
            {
                logBox.Text = L.T("没有找到日志文件。");
            }
            if (findingList != null && findingList.Items.Count == 0)
            {
                Analyze(host);
            }
        }

        private void LoadSelected()
        {
            if (logList == null || logBox == null || logList.SelectedItems.Count == 0) return;
            string path = logList.SelectedItems[0].Tag as string;
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                List<string> lines = ManifestStore.ReadTail(path, 500);
                logBox.Text = string.Join("\r\n", lines.ToArray());
                logBox.SelectionStart = logBox.Text.Length;
                logBox.ScrollToCaret();
            }
            catch (Exception error)
            {
                logBox.Text = L.T("读取失败：") + error.Message;
            }
        }

        private void Analyze(MainForm host)
        {
            if (host.Store == null || findingList == null) return;
            List<LogFinding> findings = host.Store.AnalyzeLogs();
            findingList.BeginUpdate();
            findingList.Items.Clear();
            if (findings.Count == 0)
            {
                ListViewItem none = new ListViewItem("OK");
                none.SubItems.Add(L.T("未命中常见错误"));
                none.SubItems.Add("0");
                none.SubItems.Add(L.T("仍可查看完整日志，或导出诊断包"));
                findingList.Items.Add(none);
            }
            else
            {
                foreach (LogFinding finding in findings)
                {
                    ListViewItem item = new ListViewItem(finding.Severity);
                    item.SubItems.Add(L.T(finding.Title));
                    item.SubItems.Add(finding.Count.ToString());
                    item.SubItems.Add(L.T(finding.Suggestion));
                    item.Tag = finding;
                    item.ToolTipText = L.T(finding.Title) + "\r\n" + L.T(finding.Suggestion);
                    item.ForeColor = finding.Severity == "ERROR" ? Ui.Danger : finding.Severity == "WARN" ? Ui.Warn : Ui.SecondaryInk;
                    findingList.Items.Add(item);
                }
            }
            findingList.EndUpdate();
        }

        private void OpenFolder(MainForm host)
        {
            string selected = null;
            if (logList != null && logList.SelectedItems.Count > 0)
            {
                selected = logList.SelectedItems[0].Tag as string;
            }
            host.OpenLogFolder(selected);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            return (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB";
        }
    }
}

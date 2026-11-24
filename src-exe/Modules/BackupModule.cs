using System;
using System.Drawing;
using System.Windows.Forms;

namespace DshExternalTools.Modules
{
    /// <summary>List and restore package.json backups.</summary>
    public class BackupModule : ToolModule
    {
        public override string Id { get { return "backup"; } }
        public override string Title { get { return "备份恢复"; } }
        public override string Description { get { return "恢复 package.json，恢复前自动备份。"; } }
        public override string Category { get { return "急救"; } }
        public override string IconId { get { return "backup"; } }
        public override Color Accent { get { return Ui.Accent; } }

        private ListView list;
        private Label hint;

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
            toolbar.BackColor = Ui.SurfaceFill;
            Ui.Label(toolbar, "package.json 备份", 0, 8, 260, 32, 12F, FontStyle.Bold, Ui.Ink, false);
            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Right;
            actions.Width = 360;
            actions.BackColor = Ui.SurfaceFill;
            actions.WrapContents = false;
            Ui.Button(actions, "刷新", 0, 0, 78, 32, ButtonKind.Secondary, delegate { host.RefreshState(); });
            Ui.Button(actions, "恢复选中", 0, 0, 96, 32, ButtonKind.Primary, delegate { Restore(host); });
            Ui.Button(actions, "打开备份目录", 0, 0, 116, 32, ButtonKind.Secondary, delegate { if (host.Store != null) host.OpenFolder(host.Store.BackupDir); });
            toolbar.Controls.Add(actions);
            layout.Controls.Add(toolbar, 0, 0);

            Panel card = Ui.Surface();
            card.Dock = DockStyle.Fill;
            layout.Controls.Add(card, 0, 1);

            TableLayoutPanel inner = new TableLayoutPanel();
            inner.Dock = DockStyle.Fill;
            inner.ColumnCount = 1;
            inner.RowCount = 2;
            inner.BackColor = Ui.SurfaceFill;
            inner.Padding = new Padding(14, 12, 14, 8);
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            card.Controls.Add(inner);

            list = Ui.List(inner);
            Ui.Column(list, "备份文件", 360);
            Ui.Column(list, "大小", 80);
            Ui.Column(list, "时间", 170);
            list.DoubleClick += delegate { Restore(host); };

            hint = Ui.Hint(inner, "保留最近 10 份；恢复前会先备份当前 package.json。", 0, 0, 10, 10);
            hint.Dock = DockStyle.Fill;
            hint.TextAlign = ContentAlignment.MiddleLeft;

            inner.Controls.Add(list, 0, 0);
            inner.Controls.Add(hint, 0, 1);
            return root;
        }

        public override void Refresh(MainForm host)
        {
            if (list == null) return;
            list.BeginUpdate();
            list.Items.Clear();
            if (host.Store == null)
            {
                list.EndUpdate();
                return;
            }
            foreach (BackupInfo backup in host.Store.ListBackups())
            {
                ListViewItem item = new ListViewItem(backup.Name);
                item.SubItems.Add(FormatBytes(backup.Bytes));
                item.SubItems.Add(backup.Modified.ToString("yyyy-MM-dd HH:mm:ss"));
                item.Tag = backup;
                list.Items.Add(item);
            }
            list.EndUpdate();
            if (hint != null)
            {
                hint.Text = list.Items.Count == 0
                    ? L.T("还没有备份；禁用、安全模式、恢复操作都会自动生成。")
                    : (L.IsEnglish
                        ? list.Items.Count + " backup(s). The current package.json is backed up before restoring."
                        : "共 " + list.Items.Count + " 份；恢复前会先备份当前 package.json。");
            }
        }

        private void Restore(MainForm host)
        {
            if (list.SelectedItems.Count == 0)
            {
                MessageBox.Show(host, L.T("请先选择一个备份。"), L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            BackupInfo backup = list.SelectedItems[0].Tag as BackupInfo;
            if (backup != null) host.RestoreBackup(backup);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            return (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB";
        }
    }
}

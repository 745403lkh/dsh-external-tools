using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace DshExternalTools.Modules
{
    /// <summary>Inspect and toggle profile bundles, including this plugin itself.</summary>
    public class PluginModule : ToolModule
    {
        public override string Id { get { return "plugins"; } }
        public override string Title { get { return "插件控制"; } }
        public override string Description { get { return "查看所有插件，禁用可疑项。"; } }
        public override string Category { get { return "急救"; } }
        public override string IconId { get { return "plugin"; } }
        public override Color Accent { get { return Ui.Accent; } }

        private Label state;
        private Button disableSelf;
        private Button enableSelf;
        private TextBox filter;
        private ListView list;
        private Label hint;

        public override Panel CreateView(MainForm host)
        {
            Panel root = new Panel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Ui.Window;
            root.AutoScroll = true;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.BackColor = Ui.Window;
            layout.Padding = new Padding(0);
            layout.Margin = new Padding(0);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 108F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(layout);

            Panel summary = Ui.Surface();
            summary.Dock = DockStyle.Fill;
            summary.Margin = new Padding(0, 0, 0, 14);
            layout.Controls.Add(summary, 0, 0);

            Ui.Title(summary, "DSH 插件", 18, 13, 300, 24);
            state = Ui.Label(summary, "—", 18, 40, 240, 30, 15F, FontStyle.Bold, Ui.Ink, false);
            Ui.Hint(summary, L.IsEnglish ? "Writes config only; restart DSH to apply. The standalone EXE is unaffected." : "禁用只写配置，重启 DSH 生效；独立 EXE 不受影响。", 18, 72, 480, 22);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Right;
            actions.Width = 280;
            actions.Padding = new Padding(0, 36, 10, 0);
            actions.BackColor = Ui.SurfaceFill;
            actions.WrapContents = false;
            summary.Controls.Add(actions);
            disableSelf = Ui.Button(actions, "禁用 DSH 插件", 0, 0, 130, 34, ButtonKind.Danger, delegate { Self(host, false); });
            enableSelf = Ui.Button(actions, "启用 DSH 插件", 0, 0, 130, 34, ButtonKind.Primary, delegate { Self(host, true); });

            Panel listCard = Ui.Surface();
            listCard.Dock = DockStyle.Fill;
            layout.Controls.Add(listCard, 0, 1);

            TableLayoutPanel inner = new TableLayoutPanel();
            inner.Dock = DockStyle.Fill;
            inner.ColumnCount = 1;
            inner.RowCount = 3;
            inner.BackColor = Ui.SurfaceFill;
            inner.Padding = new Padding(14, 12, 14, 8);
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            listCard.Controls.Add(inner);

            Panel toolbar = new Panel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.BackColor = Ui.SurfaceFill;
            Ui.Label(toolbar, "筛选", 0, 11, 44, 20, 8.5F, FontStyle.Regular, Ui.SecondaryInk, false);
            filter = new TextBox();
            filter.Left = 52;
            filter.Top = 8;
            filter.Width = 166;
            filter.BorderStyle = BorderStyle.FixedSingle;
            filter.Font = new Font(Ui.FontFamily, 9F);
            filter.TextChanged += delegate { Populate(host); };
            toolbar.Controls.Add(filter);

            FlowLayoutPanel tools = new FlowLayoutPanel();
            tools.Dock = DockStyle.Right;
            tools.Width = 380;
            tools.BackColor = Ui.SurfaceFill;
            tools.WrapContents = false;
            Ui.Button(tools, "刷新", 0, 0, 72, 32, ButtonKind.Secondary, delegate { host.RefreshState(); });
            Ui.Button(tools, "启用选中", 0, 0, 92, 32, ButtonKind.Secondary, delegate { ToggleSelected(host, true); });
            Ui.Button(tools, "禁用选中", 0, 0, 92, 32, ButtonKind.Danger, delegate { ToggleSelected(host, false); });
            Ui.Button(tools, "打开目录", 0, 0, 84, 32, ButtonKind.Secondary, delegate { if (host.Store != null) host.OpenFolder(host.Store.ProfileDir); });
            toolbar.Controls.Add(tools);

            list = Ui.List(inner);
            Ui.Column(list, "状态", 56);
            Ui.Column(list, "插件", 330);
            Ui.Column(list, "版本", 100);
            Ui.Column(list, "备注", 220);
            list.DoubleClick += delegate
            {
                BundleInfo selected = Selected();
                if (selected != null) host.SetBundle(selected.Name, !selected.Enabled);
            };

            TableLayoutPanel bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.ColumnCount = 2;
            bottom.RowCount = 1;
            bottom.Padding = new Padding(0);
            bottom.Margin = new Padding(0);
            bottom.BackColor = Ui.SurfaceFill;
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 304F));

            hint = Ui.Hint(bottom, "", 0, 0, 10, 10);
            hint.Dock = DockStyle.Fill;
            hint.TextAlign = ContentAlignment.MiddleLeft;

            FlowLayoutPanel rowActions = new FlowLayoutPanel();
            rowActions.Dock = DockStyle.Fill;
            rowActions.Padding = new Padding(0, 6, 0, 0);
            rowActions.BackColor = Ui.SurfaceFill;
            rowActions.WrapContents = false;
            Ui.Button(rowActions, "插件详情", 0, 0, 84, 32, ButtonKind.Secondary, delegate { DetailSelected(host); });
            Ui.Button(rowActions, "更新插件", 0, 0, 96, 32, ButtonKind.Primary, delegate { UpdateSelected(host); });
            Ui.Button(rowActions, "打开目录", 0, 0, 84, 32, ButtonKind.Secondary, delegate { OpenSelected(host); });
            bottom.Controls.Add(hint, 0, 0);
            bottom.Controls.Add(rowActions, 1, 0);

            inner.Controls.Add(toolbar, 0, 0);
            inner.Controls.Add(list, 0, 1);
            inner.Controls.Add(bottom, 0, 2);
            return root;
        }

        public override void Refresh(MainForm host)
        {
            if (host.Snapshot == null)
            {
                state.Text = L.T("未找到 profile");
                state.ForeColor = Ui.Danger;
                disableSelf.Enabled = false;
                enableSelf.Enabled = false;
                list.Items.Clear();
                hint.Text = L.T("请到“修复工具”选择包含 package.json 的 profile 目录。");
                return;
            }
            state.Text = host.Snapshot.Enabled ? L.T("已启用") : L.T("已禁用");
            state.ForeColor = host.Snapshot.Enabled ? Ui.Success : Ui.Danger;
            disableSelf.Enabled = host.Snapshot.Enabled;
            enableSelf.Enabled = !host.Snapshot.Enabled;
            Populate(host);
        }

        private void Self(MainForm host, bool enabled)
        {
            host.SetBundle("dsh-external-tools", enabled);
        }

        private void ToggleSelected(MainForm host, bool enabled)
        {
            BundleInfo selected = Selected();
            if (selected == null)
            {
                MessageBox.Show(host, L.T("请先在列表中选择一个插件。"), L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            host.SetBundle(selected.Name, enabled);
        }

        private BundleInfo Selected()
        {
            if (list == null || list.SelectedItems.Count == 0) return null;
            return list.SelectedItems[0].Tag as BundleInfo;
        }

        private void Populate(MainForm host)
        {
            if (list == null) return;
            list.BeginUpdate();
            list.Items.Clear();
            if (host.Snapshot == null)
            {
                list.EndUpdate();
                return;
            }
            string needle = filter == null ? "" : filter.Text.Trim().ToLowerInvariant();
            int shown = 0;
            foreach (BundleInfo bundle in host.Snapshot.Bundles)
            {
                string display = string.IsNullOrEmpty(bundle.DisplayName) ? bundle.Name : bundle.DisplayName;
                string haystack = (bundle.Name + " " + display + " " + (bundle.Description ?? "")).ToLowerInvariant();
                if (needle != "" && haystack.IndexOf(needle, StringComparison.Ordinal) < 0) continue;
                ListViewItem item = new ListViewItem(bundle.Enabled ? L.T("启用") : L.T("禁用"));
                item.SubItems.Add(display);
                item.SubItems.Add(bundle.Version);
                item.SubItems.Add(Notes(bundle));
                item.Tag = bundle;
                string tip = display + "  (" + bundle.Name + ")\r\n" + bundle.Version + " · " + Notes(bundle);
                if (!string.IsNullOrEmpty(bundle.Description)) tip += "\r\n" + bundle.Description;
                item.ToolTipText = tip;
                item.ForeColor = bundle.Enabled ? Ui.Ink : Ui.Muted;
                if (bundle.Self) item.BackColor = Ui.AccentSoft;
                list.Items.Add(item);
                shown++;
            }
            list.EndUpdate();
            hint.Text = L.IsEnglish
                    ? "Showing " + shown + " plugin(s). Hover for package id/description; use Details or Update below."
                    : "显示 " + shown + " 个；悬停看包名/说明；下方可查看详情或更新。";
        }

        private void DetailSelected(MainForm host)
        {
            BundleInfo selected = Selected();
            if (selected == null)
            {
                MessageBox.Show(host, L.T("请先在列表中选择一个插件。"), L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (PluginDetailsForm form = new PluginDetailsForm(selected))
            {
                form.ShowDialog(host);
            }
        }

        private void OpenSelected(MainForm host)
        {
            BundleInfo selected = Selected();
            if (selected == null)
            {
                MessageBox.Show(host, L.T("请先在列表中选择一个插件。"), L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (string.IsNullOrEmpty(selected.PackagePath) || !Directory.Exists(selected.PackagePath))
            {
                MessageBox.Show(host, L.IsEnglish ? "This bundle has no local package directory." : "这个插件没有本地包目录（可能随 DSH 内置）。",
                    L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            host.OpenFolder(selected.PackagePath);
        }

        private void UpdateSelected(MainForm host)
        {
            BundleInfo selected = Selected();
            if (selected == null)
            {
                MessageBox.Show(host, L.T("请先在列表中选择一个插件。"), L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (selected.Self)
            {
                MessageBox.Show(host, L.IsEnglish ? "This is the current EXE bundle; update it from its source folder." : "这是当前 EXE 自身的 bundle，请在源目录更新。",
                    L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (selected.Builtin || selected.Official)
            {
                MessageBox.Show(host, L.IsEnglish ? "Official DSH bundles update with DSH, not through the plugin npm channel." : "官方 DSH bundle 随 DSH 更新，不走插件 npm 更新。",
                    L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string spec = selected.DependencySpec ?? "";
            if (spec == "" || spec.StartsWith("link:", StringComparison.OrdinalIgnoreCase) ||
                spec.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
                spec.StartsWith("workspace:", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(host, L.IsEnglish
                        ? "This plugin is not installed from a registry spec, so npm cannot update it.\n\nSpec: " + (spec == "" ? "(none)" : spec)
                        : "这个插件不是 npm registry 安装的，无法用 npm 更新。\n\n声明：" + (spec == "" ? "（无）" : spec),
                    L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(host, L.IsEnglish
                    ? "Update " + selected.DisplayName + " (" + selected.Name + ") to the latest npm version?\n\nA profile package.json backup is created first. DSH should be restarted afterwards."
                    : "将 " + selected.DisplayName + "（" + selected.Name + "）更新到 npm 最新版？\n\n会先备份 profile 的 package.json；更新后建议重启 DSH。",
                L.T("更新插件"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            string backup;
            try
            {
                backup = host.Store == null ? "" : host.Store.BackupManifest();
            }
            catch (Exception error)
            {
                MessageBox.Show(host, L.IsEnglish ? "Could not create the backup: " + error.Message : "备份失败，未执行更新：" + error.Message,
                    L.T("更新插件"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            System.Windows.Forms.Cursor previous = host.Cursor;
            host.Cursor = Cursors.WaitCursor;
            try
            {
                string comspec = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                ProcessStartInfo info = new ProcessStartInfo(comspec);
                info.Arguments = "/d /s /c npm install " + selected.Name + "@latest --save --no-audit --no-fund --package-lock=false 2>&1";
                info.WorkingDirectory = host.Store.ProfileDir;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.StandardOutputEncoding = Encoding.UTF8;
                using (Process process = Process.Start(info))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    host.RefreshState();
                    string tail = Tail(output, 2600);
                    string message = (process.ExitCode == 0
                            ? (L.IsEnglish ? "Updated. Restart DSH to apply." : "更新完成，重启 DSH 后生效。")
                            : (L.IsEnglish ? "npm exited with code " : "npm 退出码 ") + process.ExitCode) +
                        "\r\n\r\n" + (string.IsNullOrEmpty(backup) ? "" : (L.IsEnglish ? "Backup: " : "备份：") + backup) +
                        (tail == "" ? "" : "\r\n\r\n" + tail);
                    MessageBox.Show(host, message, L.T("更新插件"),
                        MessageBoxButtons.OK, process.ExitCode == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(host, (L.IsEnglish ? "Update failed: " : "更新失败：") + error.Message,
                    L.T("更新插件"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                host.Cursor = previous;
            }
        }

        private static string Tail(string text, int maximum)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Trim();
            return text.Length <= maximum ? text : "…" + text.Substring(text.Length - maximum);
        }
        private static string Notes(BundleInfo bundle)
        {
            List<string> notes = new List<string>();
            if (bundle.Self) notes.Add(L.T("本插件"));
            if (bundle.Protected) notes.Add(L.T("受保护"));
            notes.Add(bundle.Official ? L.T("官方") : L.T("第三方"));
            if (bundle.Builtin) notes.Add(L.T("内置"));
            else if (!bundle.Installed) notes.Add(L.T("未安装"));
            else notes.Add(L.T("已安装"));
            return string.Join(" / ", notes.ToArray());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DshExternalTools.Modules;

namespace DshExternalTools
{
    /// <summary>
    /// Portable rescue console: a Windows Settings-like shell with a module grid,
    /// one module view at a time, and shared profile/repair actions.
    /// </summary>
    public class MainForm : Form
    {
        /// <summary>Set by --ui-preview so startup warnings never block automation.</summary>
        public static bool SuppressStartupDialogs = false;

        private ManifestStore store;
        private ProfileSnapshot snapshot;
        private readonly List<ToolModule> modules = new List<ToolModule>();
        private readonly Dictionary<string, Panel> views = new Dictionary<string, Panel>();
        private ToolModule currentModule;
        private AboutModule aboutModule;

        private Panel homeHost;
        private Panel moduleHost;
        private Panel moduleContent;
        private Label moduleTitle;
        private Label moduleDescription;
        private StatusPill headerPill;
        private Button languageButton;
        private StatCard statPlugin;
        private StatCard statDsh;
        private StatCard statBackup;
        private StatCard statSafety;
        private Label footerProfile;
        private Label footerDsh;
        private CheckBox forceCheck;
        private bool noProfileNotified;
        private FlowLayoutPanel homeGrid;
        private readonly List<ModuleCard> homeCards = new List<ModuleCard>();

        public MainForm()
        {
            Text = (L.IsEnglish ? "DSH External Tools" : "DSH 外部工具") + " " + Program.DisplayVersion;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(1060, 720);
            MinimumSize = new Size(920, 620);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Ui.Window;
            Font = new Font(Ui.FontFamily, 9F);
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            CreateModules();
            BuildUi();
            LoadState();
            ShowHome();
        }

        private void CreateModules()
        {
            modules.Clear();
            modules.Add(new PluginModule());
            modules.Add(new SafetyModule());
            modules.Add(new BackupModule());
            modules.Add(new WebModule());
            modules.Add(new DiagnoseModule());
            modules.Add(new LogModule());
            modules.Add(new RepairModule());
            modules.Add(new ReinstallModule());
            aboutModule = new AboutModule();
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.BackColor = Ui.Window;
            root.Padding = new Padding(0);
            root.Margin = new Padding(0);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildContent(), 0, 1);
            root.Controls.Add(BuildFooter(), 0, 2);
            Controls.Add(root);
        }

        private void RebuildUi(string moduleId)
        {
            bool force = forceCheck != null && forceCheck.Checked;
            SuspendLayout();
            if (Controls.Count > 0)
            {
                Control old = Controls[0];
                Controls.Clear();
                old.Dispose();
            }
            views.Clear();
            homeCards.Clear();
            homeGrid = null;
            headerPill = null;
            languageButton = null;
            forceCheck = null;
            statPlugin = null;
            statDsh = null;
            statBackup = null;
            statSafety = null;
            footerProfile = null;
            footerDsh = null;
            currentModule = null;
            noProfileNotified = true;
            Text = (L.IsEnglish ? "DSH External Tools" : "DSH 外部工具") + " " + Program.DisplayVersion;
            CreateModules();
            BuildUi();
            if (forceCheck != null) forceCheck.Checked = force;
            LoadState();
            if (!string.IsNullOrEmpty(moduleId))
            {
                if (moduleId == "about")
                {
                    ShowModule(aboutModule);
                }
                else
                {
                    ToolModule target = modules.Find(delegate (ToolModule item) { return item.Id == moduleId; });
                    if (target != null) ShowModule(target);
                }
            }
            else
            {
                ShowHome();
            }
            ResumeLayout(true);
            PerformLayout();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (SuppressStartupDialogs) return;
            DshEnvironment.Refresh();
            if (DshEnvironment.State == DshVersionState.Missing)
            {
                MessageBox.Show(this,
                    L.IsEnglish
                        ? "No verifiable DSH installation was found.\n\nWeb DSH is unavailable; Reinstall can still open the installer."
                        : "未找到可验证的 DSH 安装。\n\nWeb 端启动不可用；重装仍可打开安装包进行安装。",
                    L.T("DSH 版本校验"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            else if (DshEnvironment.State == DshVersionState.Mismatched)
            {
                MessageBox.Show(this,
                    L.IsEnglish
                        ? "DSH version does not match this tool.\n\nInstalled: " + (DshEnvironment.InstalledVersion ?? "unknown") +
                          "\nVerified: " + DshEnvironment.SupportedLabel +
                          "\n\nReinstall is disabled to avoid installing the wrong version; Web DSH can still be tried."
                        : "DSH 版本与当前工具不匹配。\n\n当前安装: " + (DshEnvironment.InstalledVersion ?? "未知") +
                          "\n本工具验证: " + DshEnvironment.SupportedLabel +
                          "\n\n为避免装错版本，重装功能已禁用；Web 端仍可尝试启动。",
                    L.T("DSH 版本不匹配"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int caption = 0x00FFFFFF;
                int text = 0x001B1B1B;
                int border = 0x00E5E5E5;
                DwmSetWindowAttribute(Handle, 35, ref caption, 4);
                DwmSetWindowAttribute(Handle, 36, ref text, 4);
                DwmSetWindowAttribute(Handle, 34, ref border, 4);
            }
            catch
            {
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private Panel BuildHeader()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Ui.SurfaceFill;
            header.Padding = new Padding(24, 10, 24, 6);

            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null)
                {
                    PictureBox logo = new PictureBox();
                    logo.Left = 24;
                    logo.Top = 16;
                    logo.Width = 40;
                    logo.Height = 40;
                    logo.SizeMode = PictureBoxSizeMode.Zoom;
                    logo.Image = icon.ToBitmap();
                    header.Controls.Add(logo);
                }
            }
            catch { }

            Ui.Label(header, "外部工具", 76, 2, 420, 36, 18F, FontStyle.Bold, Ui.Ink, false);
            Ui.Label(header, "DSH 急救与调试台 · 便携 EXE · 不加载 DSH", 78, 44, 600, 22, 9F, FontStyle.Regular, Ui.Muted, false);

            Panel right = new Panel();
            right.Dock = DockStyle.Right;
            right.Width = 330;
            right.BackColor = Ui.SurfaceFill;
            header.Controls.Add(right);

            headerPill = new StatusPill();
            headerPill.Left = 0;
            headerPill.Top = 22;
            headerPill.Width = 108;
            headerPill.Height = 24;
            headerPill.Text = L.T("读取中");
            right.Controls.Add(headerPill);

            Ui.Button(right, "关于", 154, 10, 160, 32, ButtonKind.Secondary, delegate { ShowModule(aboutModule); });
            languageButton = Ui.Button(right, "语言/Language", 154, 50, 160, 32, ButtonKind.Secondary, delegate { OpenLanguageMenu(); });

            forceCheck = new CheckBox();
            forceCheck.Text = L.IsEnglish ? "Force write" : "运行时强制写入";
            forceCheck.Left = 0;
            forceCheck.Top = 48;
            forceCheck.Width = 150;
            forceCheck.Height = 30;
            forceCheck.Font = new Font(Ui.FontFamily, 8.5F, FontStyle.Regular);
            forceCheck.ForeColor = Ui.SecondaryInk;
            forceCheck.BackColor = Ui.SurfaceFill;
            forceCheck.CheckAlign = ContentAlignment.MiddleLeft;
            right.Controls.Add(forceCheck);
            ToolTip forceTip = new ToolTip();
            forceTip.SetToolTip(forceCheck, L.IsEnglish ? "Allow writes while DSH is running" : "DSH 运行时允许强制写入");

            return header;
        }

        private void OpenLanguageMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font(Ui.FontFamily, 9F);

            ToolStripMenuItem system = new ToolStripMenuItem("跟随系统 / System");
            system.Checked = L.Mode == L.LanguageMode.System;
            system.Click += delegate { ApplyLanguage(L.LanguageMode.System); };
            menu.Items.Add(system);

            ToolStripMenuItem chinese = new ToolStripMenuItem("简体中文");
            chinese.Checked = L.Mode == L.LanguageMode.Chinese;
            chinese.Click += delegate { ApplyLanguage(L.LanguageMode.Chinese); };
            menu.Items.Add(chinese);

            ToolStripMenuItem english = new ToolStripMenuItem("English");
            english.Checked = L.Mode == L.LanguageMode.English;
            english.Click += delegate { ApplyLanguage(L.LanguageMode.English); };
            menu.Items.Add(english);

            menu.Show(languageButton, new Point(0, languageButton.Height));
        }

        private void ApplyLanguage(L.LanguageMode mode)
        {
            string moduleId = currentModule == null ? null : currentModule.Id;
            L.SetMode(mode);
            RebuildUi(moduleId);
        }

        /// <summary>Used by --ui-preview to exercise the in-place language rebuild.</summary>
        public void SwitchLanguageForTest(string code)
        {
            ApplyLanguage(code == "en" ? L.LanguageMode.English : L.LanguageMode.Chinese);
        }

        private Panel BuildContent()
        {
            Panel shell = new Panel();
            shell.Dock = DockStyle.Fill;
            shell.BackColor = Ui.Window;
            shell.Padding = new Padding(16, 0, 16, 12);

            Panel card = Ui.Surface();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(24, 20, 24, 20);
            shell.Controls.Add(card);

            moduleHost = new Panel();
            moduleHost.Dock = DockStyle.Fill;
            moduleHost.BackColor = Ui.SurfaceFill;
            moduleHost.Visible = false;

            Panel moduleHeader = new Panel();
            moduleHeader.Dock = DockStyle.Top;
            moduleHeader.Height = 62;
            moduleHeader.BackColor = Ui.SurfaceFill;
            Ui.Button(moduleHeader, "← 返回", 0, 12, 86, 32, ButtonKind.Secondary, delegate { ShowHome(); });
            moduleTitle = Ui.Label(moduleHeader, "", 100, 2, 700, 32, 16F, FontStyle.Bold, Ui.Ink, false);
            moduleDescription = Ui.Label(moduleHeader, "", 102, 36, 760, 20, 9F, FontStyle.Regular, Ui.Muted, true);
            moduleHost.Controls.Add(moduleHeader);

            moduleContent = new Panel();
            moduleContent.Dock = DockStyle.Fill;
            moduleContent.BackColor = Ui.SurfaceFill;
            moduleContent.Padding = new Padding(0, 8, 0, 0);
            moduleHost.Controls.Add(moduleContent);
            moduleContent.BringToFront();

            homeHost = new Panel();
            homeHost.Dock = DockStyle.Fill;
            homeHost.BackColor = Ui.SurfaceFill;
            BuildHome(homeHost);

            card.Controls.Add(moduleHost);
            card.Controls.Add(homeHost);
            homeHost.BringToFront();
            return shell;
        }

        private void BuildHome(Control host)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 3;
            layout.BackColor = Ui.SurfaceFill;
            layout.Padding = new Padding(0);
            layout.Margin = new Padding(0);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            host.Controls.Add(layout);

            TableLayoutPanel stats = new TableLayoutPanel();
            stats.Dock = DockStyle.Fill;
            stats.ColumnCount = 4;
            stats.RowCount = 1;
            stats.BackColor = Ui.SurfaceFill;
            stats.Padding = new Padding(0);
            stats.Margin = new Padding(0);
            for (int index = 0; index < 4; index++)
            {
                stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }
            statPlugin = AddStat(stats, "plugin", "DSH 插件");
            statDsh = AddStat(stats, "process", "DSH 进程");
            statBackup = AddStat(stats, "backup", "配置备份");
            statSafety = AddStat(stats, "safety", "安全模式");
            layout.Controls.Add(stats, 0, 0);

            Panel section = new Panel();
            section.Dock = DockStyle.Fill;
            section.BackColor = Ui.SurfaceFill;
            Ui.Label(section, "工具", 0, 0, 200, 28, 14F, FontStyle.Bold, Ui.Ink, false);
            Label hint = Ui.Hint(section, L.IsEnglish ? "Choose a tool to start; used tools are added here." : "选择模块开始；后续工具会自动加入这里。", 0, 30, 600, 24);
            hint.TextAlign = ContentAlignment.MiddleLeft;
            layout.Controls.Add(section, 0, 1);

            FlowLayoutPanel grid = new FlowLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.BackColor = Ui.SurfaceFill;
            grid.WrapContents = true;
            grid.AutoScroll = true;
            grid.Padding = new Padding(0);
            grid.Margin = new Padding(0);
            homeGrid = grid;
            homeCards.Clear();
            foreach (ToolModule module in modules)
            {
                ModuleCard card = new ModuleCard();
                card.Title = module.Title;
                card.Description = module.Description;
                card.IconId = module.IconId;
                card.Category = module.Category;
                card.Size = new Size(300, 116);
                card.Margin = new Padding(0, 0, 14, 14);
                ToolModule captured = module;
                card.Activated += delegate { ShowModule(captured); };
                grid.Controls.Add(card);
                homeCards.Add(card);
            }
            layout.Controls.Add(grid, 0, 2);
            host.Resize += delegate { LayoutHomeCards(); };
            LayoutHomeCards();
        }

        private void LayoutHomeCards()
        {
            if (homeGrid == null || homeCards.Count == 0) return;
            int width = homeGrid.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;
            if (width < 320) width = homeGrid.ClientSize.Width;
            int gap = 14;
            int columns = width >= 900 ? 3 : 2;
            int cardWidth = (width - gap * columns - 4) / columns;
            if (cardWidth < 240) cardWidth = Math.Max(220, width - 4);
            foreach (ModuleCard card in homeCards)
            {
                card.Width = cardWidth;
                card.Height = 116;
                card.Margin = new Padding(0, 0, gap, gap);
            }
            homeGrid.PerformLayout();
        }

        private StatCard AddStat(TableLayoutPanel stats, string icon, string caption)
        {
            StatCard card = new StatCard();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 0, 12, 8);
            card.IconId = icon;
            card.Caption = caption;
            card.Value = "—";
            stats.Controls.Add(card);
            return card;
        }

        private Panel BuildFooter()
        {
            Panel footer = new Panel();
            footer.Dock = DockStyle.Fill;
            footer.BackColor = Ui.Window;
            footer.Padding = new Padding(24, 0, 24, 0);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 1;
            layout.BackColor = Ui.Window;
            layout.Padding = new Padding(0);
            layout.Margin = new Padding(0);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            footer.Controls.Add(layout);

            footerProfile = Ui.Label(layout, "", 0, 0, 10, 10, 8F, FontStyle.Regular, Ui.Muted, true);
            footerProfile.Dock = DockStyle.Fill;
            footerProfile.Height = 28;

            footerDsh = Ui.Label(layout, "", 0, 0, 10, 10, 8F, FontStyle.Regular, Ui.Muted, false);
            footerDsh.Dock = DockStyle.Fill;
            footerDsh.TextAlign = ContentAlignment.MiddleRight;
            layout.Controls.Add(footerProfile, 0, 0);
            layout.Controls.Add(footerDsh, 1, 0);
            return footer;
        }

        internal ManifestStore Store { get { return store; } }
        internal ProfileSnapshot Snapshot { get { return snapshot; } }
        internal bool ForceWrite { get { return forceCheck != null && forceCheck.Checked; } }

        internal void LoadState()
        {
            try
            {
                DshEnvironment.Refresh();
                store = new ManifestStore(null);
                if (!File.Exists(store.ManifestPath))
                {
                    snapshot = null;
                    UpdateHeaderAndFooter();
                    if (!noProfileNotified)
                    {
                        noProfileNotified = true;
                        MessageBox.Show(this,
                            L.IsEnglish
                                ? "Profile configuration was not found:\n" + store.ManifestPath +
                                  "\n\nChoose a directory containing package.json in Repair tools."
                                : "没有找到 profile 配置：\n" + store.ManifestPath +
                                  "\n\n请在“修复工具”中选择包含 package.json 的目录。",
                            L.T("未找到 profile"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    return;
                }
                snapshot = store.Describe();
                UpdateHeaderAndFooter();
                if (currentModule != null) currentModule.Refresh(this);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "读取失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateHeaderAndFooter()
        {
            if (headerPill != null)
            {
                if (snapshot == null)
                {
                    headerPill.Text = L.T("未找到 profile");
                    headerPill.Tone = Ui.Danger;
                }
                else if (snapshot.Enabled)
                {
                    headerPill.Text = L.T("已启用");
                    headerPill.Tone = Ui.Success;
                }
                else
                {
                    headerPill.Text = L.T("已禁用");
                    headerPill.Tone = Ui.Danger;
                }
                headerPill.Invalidate();
            }
            if (statPlugin != null)
            {
                statPlugin.Value = snapshot == null ? "—" : L.T(snapshot.Enabled ? "已启用" : "已禁用");
                statDsh.Value = snapshot == null ? "—" : (L.IsEnglish
                    ? snapshot.DshProcessCount + " running"
                    : snapshot.DshProcessCount + " 个");
                statBackup.Value = snapshot == null ? "—" : (L.IsEnglish
                    ? snapshot.BackupCount + (snapshot.BackupCount == 1 ? " backup" : " backups")
                    : snapshot.BackupCount + " 份");
                statSafety.Value = snapshot == null ? "—" : L.T(snapshot.SafeMode ? "已开启" : "正常");
            }
            if (footerProfile != null)
            {
                footerProfile.Text = snapshot == null ? "未找到 profile" : "profile: " + snapshot.ProfileDir;
                footerProfile.Tag = snapshot == null ? null : snapshot.ManifestPath;
            }
            if (footerDsh != null)
            {
                string dsh = DshEnvironment.InstalledVersion == null
                    ? "DSH 未找到"
                    : "DSH " + DshEnvironment.InstalledVersion;
                if (DshEnvironment.State == DshVersionState.Mismatched) dsh += "（不匹配）";
                footerDsh.Text = "v" + Program.DisplayVersion + " · " + dsh;
                footerDsh.ForeColor = DshEnvironment.State == DshVersionState.Mismatched ? Ui.Danger : Ui.Muted;
            }
        }

        internal void RefreshState()
        {
            LoadState();
            UpdateHeaderAndFooter();
        }

        internal void ShowHome()
        {
            currentModule = null;
            if (moduleHost != null) moduleHost.Visible = false;
            if (homeHost != null) homeHost.Visible = true;
            LoadState();
            if (homeHost != null) homeHost.BringToFront();
        }

        internal void ShowModule(ToolModule module)
        {
            if (module == null) return;
            currentModule = module;
            homeHost.Visible = false;
            moduleHost.Visible = true;
            moduleTitle.Text = L.T(module.Title);
            moduleDescription.Text = L.T(module.Description);
            moduleContent.Controls.Clear();

            Panel view;
            if (!views.TryGetValue(module.Id, out view))
            {
                view = module.CreateView(this);
                views[module.Id] = view;
            }
            view.Dock = DockStyle.Fill;
            moduleContent.Controls.Add(view);
            moduleContent.BringToFront();
            try { module.Refresh(this); }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "模块加载失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void SelectModule(int index)
        {
            if (index <= 0) ShowHome();
            else if (index <= modules.Count) ShowModule(modules[index - 1]);
            else ShowModule(aboutModule);
        }

        internal bool AllowWrite()
        {
            if (ManifestStore.DshProcessCount() == 0) return true;
            if (!ForceWrite)
            {
                MessageBox.Show(this,
                    L.IsEnglish
                        ? "DSH is running. Completely exit DeepSeek Harness first,\nor tick \"Force write\" in the header."
                        : "DSH 正在运行。请先完全退出 DeepSeek Harness，\n或在顶部勾选“DSH 运行时强制写入”。",
                    L.T("DSH 正在运行"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return MessageBox.Show(this,
                L.IsEnglish
                    ? "DSH is still running; a forced write may be overwritten by the running instance.\nContinue?"
                    : "DSH 仍在运行，强制写入可能被运行中的实例覆盖。\n确定继续？",
                L.T("强制写入"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        internal void SetBundle(string name, bool enabled)
        {
            BundleInfo bundle = FindBundle(name);
            if (bundle == null)
            {
                MessageBox.Show(this,
                    L.IsEnglish ? "Plugin not found: " + name : "没有找到插件：" + name,
                    L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!enabled && bundle.Protected)
            {
                MessageBox.Show(this,
                    L.IsEnglish
                        ? name + " is a protected built-in plugin and cannot be disabled."
                        : name + " 是受保护的内置插件，不能禁用。",
                    L.T("受保护"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!enabled && MessageBox.Show(this,
                L.IsEnglish
                    ? "Disable " + name + "?\n\nA restart of DSH is required for the change to take effect."
                    : "确定禁用 " + name + "？\n\n写入后需要重启 DSH 才生效。",
                L.T("确认禁用"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            if (!AllowWrite()) return;
            try
            {
                bool changed = store.SetEnabled(name, enabled, ForceWrite);
                RefreshState();
                if (changed)
                {
                    MessageBox.Show(this,
                        L.IsEnglish
                            ? (enabled ? "Enabled " : "Disabled ") + name + ".\nRestart DSH to apply."
                            : (enabled ? "已启用 " : "已禁用 ") + name + "。\n重启 DSH 后生效。",
                        L.T("完成"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(this, L.T("状态没有变化。"), L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, L.T("操作失败"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal void RestoreBackup(BackupInfo backup)
        {
            if (backup == null) return;
            if (MessageBox.Show(this,
                "恢复 " + backup.Name + "？\n\n当前 package.json 会先备份。恢复后需要重启 DSH。",
                "确认恢复", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            if (!AllowWrite()) return;
            try
            {
                store.RestoreBackup(backup.Path, ForceWrite);
                RefreshState();
                MessageBox.Show(this, "已恢复备份。请重启 DSH 验证。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "恢复失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal void ResetPatch()
        {
            if (MessageBox.Show(this,
                "把 cordis.patch.yml 重置为 []？\n\n会先备份原文件。这会清空用户 patch 层，但不会删除 bundle。",
                "重置 patch 层", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            if (!AllowWrite()) return;
            try
            {
                store.ResetPatchLayer();
                RefreshState();
                MessageBox.Show(this, "cordis.patch.yml 已重置为 []。\n原文件已备份。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "重置失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal void ToggleSafeMode(bool startAfter)
        {
            if (snapshot == null) return;
            if (snapshot.SafeMode)
            {
                if (MessageBox.Show(this, "退出安全模式并恢复之前停用的插件？\n\n当前配置会先备份。",
                    "退出安全模式", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }
                if (!AllowWrite()) return;
                try
                {
                    string message = store.ExitSafeMode(ForceWrite);
                    RefreshState();
                    MessageBox.Show(this, message, "已退出安全模式", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    if (startAfter) StartDsh();
                }
                catch (Exception error)
                {
                    MessageBox.Show(this, error.Message, "退出失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            if (MessageBox.Show(this,
                "进入安全模式？\n\n只保留 dsh-base / dsh-web-app，其余 bundle 临时移出启动列表。\n写入前会备份，之后可以一键恢复。",
                "进入安全模式", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            if (!AllowWrite()) return;
            try
            {
                string message = store.EnterSafeMode(ForceWrite);
                RefreshState();
                MessageBox.Show(this, message + "\n\n现在可以启动 DSH 验证。", "已进入安全模式", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (startAfter) StartDsh();
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "进入失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal void OpenPnpmTerminal()
        {
            if (store == null) return;
            if (MessageBox.Show(this,
                "将打开命令提示符，在 profile 目录执行：\n\npnpm install --prefer-offline\n\n可能需要几分钟并访问网络。继续？",
                "修复依赖", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
            try
            {
                ProcessStartInfo info = new ProcessStartInfo("cmd.exe");
                info.Arguments = "/k cd /d \"" + store.ProfileDir + "\" && pnpm install --prefer-offline";
                info.UseShellExecute = true;
                Process.Start(info);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "无法打开终端", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal void KillDsh()
        {
            int count = ManifestStore.DshProcessCount();
            if (count == 0)
            {
                MessageBox.Show(this, "没有正在运行的 DSH 进程。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this,
                "结束 " + count + " 个 DeepSeek Harness 进程？\n\n未保存的会话可能丢失。",
                "结束 DSH", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            int killed = 0;
            List<Process> processes = ManifestStore.DshProcesses();
            foreach (Process process in processes)
            {
                try { process.Kill(); killed++; }
                catch { }
                finally { process.Dispose(); }
            }
            System.Threading.Thread.Sleep(400);
            RefreshState();
            MessageBox.Show(this, "已请求结束 " + killed + " 个进程。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        internal void StartDsh()
        {
            string executable = ManifestStore.FindDshExecutable();
            if (executable == null)
            {
                MessageBox.Show(this,
                    "没有找到 DeepSeek Harness.exe。\n\n可以从开始菜单启动 DSH，或设置 DSH_NODE_EXECUTABLE 环境变量。",
                    "未找到 DSH", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(executable);
                info.UseShellExecute = true;
                info.WorkingDirectory = Path.GetDirectoryName(executable);
                Process.Start(info);
                MessageBox.Show(this, "已启动：\n" + executable, "启动 DSH", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal void ChooseProfile()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择包含 package.json 的 DSH profile 目录";
                dialog.ShowNewFolderButton = false;
                if (store != null && Directory.Exists(store.ProfileDir)) dialog.SelectedPath = store.ProfileDir;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string directory = dialog.SelectedPath;
                if (!File.Exists(Path.Combine(directory, "package.json")))
                {
                    MessageBox.Show(this, "该目录没有 package.json，不是 DSH profile。", "无效目录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                ManifestStore.SaveProfile(directory);
                LoadState();
            }
        }

        internal void OpenFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                MessageBox.Show(this, "目录不存在：" + path, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                ProcessStartInfo info = new ProcessStartInfo("explorer.exe");
                info.Arguments = "\"" + path + "\"";
                info.UseShellExecute = true;
                Process.Start(info);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal void OpenLogFolder(string selectedPath)
        {
            string path = selectedPath;
            if (string.IsNullOrEmpty(path) && store != null)
            {
                List<string> logs = ManifestStore.FindLogFiles(store.ProfileDir);
                if (logs.Count > 0) path = logs[0];
            }
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                OpenFolder(Path.GetDirectoryName(path));
                return;
            }
            if (store != null) OpenFolder(store.ProfileDir);
        }

        internal void OpenReadme()
        {
            string readme = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "README.md");
            if (!File.Exists(readme))
            {
                MessageBox.Show(this, "没有找到 README.md。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(readme);
                info.UseShellExecute = true;
                Process.Start(info);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal void CopyText(string text, string title)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                Clipboard.SetText(text);
                MessageBox.Show(this, title + "已复制。", "已复制", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "复制失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal void ExportDiagnostics(string report)
        {
            if (store == null) return;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "ZIP 文件|*.zip";
                dialog.FileName = "dsh-diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip";
                dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string temporary = Path.Combine(Path.GetTempPath(), "dsh-diag-" + Guid.NewGuid().ToString("N"));
                try
                {
                    Directory.CreateDirectory(temporary);
                    CopyIfExists(store.ManifestPath, Path.Combine(temporary, "package.json"));
                    CopyIfExists(Path.Combine(store.ProfileDir, "cordis.patch.yml"), Path.Combine(temporary, "cordis.patch.yml"));
                    CopyIfExists(Path.Combine(store.ProfileDir, "hub.log"), Path.Combine(temporary, "hub.log"));
                    foreach (string log in ManifestStore.FindLogFiles(store.ProfileDir))
                    {
                        CopyIfExists(log, Path.Combine(temporary, "log-" + Path.GetFileName(log)));
                    }
                    File.WriteAllText(Path.Combine(temporary, "diagnostics.txt"), report ?? "", new UTF8Encoding(false));

                    StringBuilder bundles = new StringBuilder();
                    if (snapshot != null)
                    {
                        foreach (BundleInfo bundle in snapshot.Bundles)
                        {
                            bundles.AppendLine((bundle.Enabled ? "[on ] " : "[off] ") + bundle.Name + "  " + bundle.Version);
                        }
                    }
                    File.WriteAllText(Path.Combine(temporary, "bundles.txt"), bundles.ToString(), new UTF8Encoding(false));

                    StringBuilder processes = new StringBuilder();
                    List<Process> running = ManifestStore.DshProcesses();
                    foreach (Process process in running)
                    {
                        try { processes.AppendLine(process.ProcessName + " pid=" + process.Id); }
                        catch { }
                        finally { process.Dispose(); }
                    }
                    File.WriteAllText(Path.Combine(temporary, "processes.txt"), processes.ToString(), new UTF8Encoding(false));

                    if (File.Exists(dialog.FileName)) File.Delete(dialog.FileName);
                    ZipFile.CreateFromDirectory(temporary, dialog.FileName);
                    MessageBox.Show(this, "诊断包已导出：\n" + dialog.FileName, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception error)
                {
                    MessageBox.Show(this, error.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    try { Directory.Delete(temporary, true); }
                    catch { }
                }
            }
        }

        private static void CopyIfExists(string source, string target)
        {
            if (File.Exists(source)) File.Copy(source, target, true);
        }

        private BundleInfo FindBundle(string name)
        {
            if (snapshot == null) return null;
            foreach (BundleInfo bundle in snapshot.Bundles)
            {
                if (string.Equals(bundle.Name, name, StringComparison.Ordinal)) return bundle;
            }
            return null;
        }
    }
}

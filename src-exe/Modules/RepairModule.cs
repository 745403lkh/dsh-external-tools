using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DshExternalTools.Modules
{
    /// <summary>Dependency, directory and profile utilities.</summary>
    public class RepairModule : ToolModule
    {
        public override string Id { get { return "repair"; } }
        public override string Title { get { return "修复工具"; } }
        public override string Description { get { return "修依赖、打开目录、切换 profile。"; } }
        public override string Category { get { return "维护"; } }
        public override string IconId { get { return "repair"; } }
        public override Color Accent { get { return Ui.Accent; } }

        private Label profilePath;
        private Panel root;

        public override Panel CreateView(MainForm host)
        {
            root = new Panel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Ui.Window;
            root.AutoScroll = true;

            Panel deps = Ui.Card(root, 0, 0, 900, 140);
            Ui.Title(deps, "依赖修复", 18, 14, 300, 24);
            Ui.Hint(deps, "插件目录缺失或 node_modules 损坏时，在 profile 目录执行 pnpm install；会打开终端。", 18, 42, 700, 38);
            Ui.Button(deps, "打开终端修依赖", 18, 80, 160, 34, ButtonKind.Primary, delegate { host.OpenPnpmTerminal(); });
            Ui.Hint(deps, "pnpm install --prefer-offline", 190, 86, 300, 24);

            Panel dirs = Ui.Card(root, 0, 154, 900, 130);
            Ui.Title(dirs, "目录", 18, 14, 300, 24);
            Ui.Hint(dirs, "排查时经常需要直接看配置文件、日志和工具目录。", 18, 42, 700, 22);
            Ui.Button(dirs, "配置目录", 18, 76, 110, 34, ButtonKind.Secondary, delegate { if (host.Store != null) host.OpenFolder(host.Store.ProfileDir); });
            Ui.Button(dirs, "日志目录", 138, 76, 110, 34, ButtonKind.Secondary, delegate { host.OpenLogFolder(null); });
            Ui.Button(dirs, "工具目录", 258, 76, 110, 34, ButtonKind.Secondary, delegate { host.OpenFolder(Path.GetDirectoryName(Application.ExecutablePath)); });
            Ui.Button(dirs, "README", 378, 76, 100, 34, ButtonKind.Secondary, delegate { host.OpenReadme(); });

            Panel profile = Ui.Card(root, 0, 298, 900, 146);
            Ui.Title(profile, "profile", 18, 14, 300, 24);
            Ui.Hint(profile, "默认读取 DSH_PROFILE_DIR / DSH_HOME 或用户目录下的 .dsh\\profiles\\desktop。", 18, 38, 700, 42);
            profilePath = Ui.Label(profile, "—", 18, 84, 600, 24, 9F, FontStyle.Regular, Ui.SecondaryInk, true);
            Ui.Button(profile, "选择 profile 目录…", 18, 110, 160, 32, ButtonKind.Secondary, delegate { host.ChooseProfile(); });

            root.Resize += delegate
            {
                int width = Math.Max(620, root.ClientSize.Width - 4);
                deps.Width = width;
                dirs.Width = width;
                profile.Width = width;
                if (profilePath != null) profilePath.Width = Math.Max(280, width - 220);
            };
            return root;
        }

        public override void Refresh(MainForm host)
        {
            if (profilePath != null)
            {
                profilePath.Text = host.Store == null ? "未找到 profile" : host.Store.ManifestPath;
            }
        }
    }
}

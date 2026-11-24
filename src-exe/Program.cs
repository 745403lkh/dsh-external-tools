using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("DSH 外部工具")]
[assembly: System.Reflection.AssemblyDescription("DSH rescue and debug console")]
[assembly: System.Reflection.AssemblyProduct("dsh-external-tools")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.15")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.15")]

namespace DshExternalTools
{
    /// <summary>One selectable profile bundle.</summary>
    public class BundleInfo
    {
        public string Name;
        public bool Enabled;
        public bool Installed;
        public bool Builtin;
        public bool Protected;
        public bool Official;
        public bool Self;
        public string Version;
        public string DisplayName;
        public string Description;
        public string PackagePath;
        public string DependencySpec;
        public string Author;
        public string License;
        public string Repository;
    }

    /// <summary>One rotated profile backup.</summary>
    public class BackupInfo
    {
        public string Name;
        public string Path;
        public long Bytes;
        public DateTime Modified;
    }

    /// <summary>Safe-mode state saved next to the profile.</summary>
    public class SafeModeInfo
    {
        public string CreatedAt;
        public List<string> Removed;
        public List<string> Previous;
    }

    /// <summary>One diagnostic finding.</summary>
    public class DiagItem
    {
        public string Level;
        public string Title;
        public string Detail;
    }

    /// <summary>A snapshot used by the window and the self-test.</summary>
    public class ProfileSnapshot
    {
        public string ProfileDir;
        public string ManifestPath;
        public string ExePath;
        public string CliPath;
        public bool Enabled;
        public bool Installed;
        public bool Protected;
        public bool DshRunning;
        public bool SafeMode;
        public int DshProcessCount;
        public int BackupCount;
        public List<BundleInfo> Bundles;
    }

    internal static class Program
    {
        public const string Version = "1.0.0-beta.15";
        public const string DisplayVersion = "1.0.0 beta15";

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "--self-test")
                {
                    return SelfTest(args);
                }
                if (args.Length > 0 && args[0] == "--diagnose")
                {
                    return Diagnose(args);
                }
                if (args.Length > 0 && args[0] == "--ui-preview")
                {
                    return UiPreview(args);
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }
            catch (Exception error)
            {
                if (args.Length > 0 && args[0] == "--ui-preview")
                {
                    try
                    {
                        File.WriteAllText(
                            Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "ui-preview-error.txt"),
                            error.ToString(),
                            new UTF8Encoding(false));
                    }
                    catch
                    {
                    }
                    return 1;
                }
                try
                {
                    MessageBox.Show(
                        "外部工具启动失败：\r\n\r\n" + error,
                        "DSH 外部工具",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                catch
                {
                }
                return 1;
            }
        }

        /// <summary>
        /// Headless verification: read the live manifest without changing it, then
        /// run disable/enable against a temporary copy and report to a JSON file.
        /// </summary>
        private static int SelfTest(string[] args)
        {
            string profileArg = null;
            string outPath = null;
            for (int index = 1; index < args.Length; index++)
            {
                if (args[index] == "--out" && index + 1 < args.Length)
                {
                    outPath = args[++index];
                }
                else if (profileArg == null)
                {
                    profileArg = args[index];
                }
            }

            Dictionary<string, object> result = new Dictionary<string, object>();
            string temporary = null;
            try
            {
                ManifestStore live = new ManifestStore(profileArg);
                ProfileSnapshot snapshot = live.Describe();
                result["profileDir"] = live.ProfileDir;
                result["manifestPath"] = live.ManifestPath;
                result["enabled"] = snapshot.Enabled;
                result["bundleCount"] = snapshot.Bundles.Count;
                result["dshProcessCount"] = snapshot.DshProcessCount;

                string text = live.ReadManifestText();
                object parsed = ManifestStore.Parse(text);
                string pretty = ManifestStore.PrettyJson(parsed, 0);
                object parsedAgain = ManifestStore.Parse(pretty);
                result["roundTrip"] = ManifestStore.PrettyJson(parsedAgain, 0) == pretty;

                temporary = Path.Combine(live.ProfileDir, ".dsh-external-tools", "selftest-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temporary);
                File.Copy(live.ManifestPath, Path.Combine(temporary, "package.json"));
                string fakeBundle = Path.Combine(temporary, "node_modules", "dsh-external-tools");
                Directory.CreateDirectory(fakeBundle);
                File.WriteAllText(Path.Combine(fakeBundle, "package.json"),
                    "{\"name\":\"dsh-external-tools\",\"version\":\"" + Version + "\"}", new UTF8Encoding(false));
                ManifestStore copy = new ManifestStore(temporary);
                copy.SetEnabled("dsh-external-tools", true, true);
                bool disabled = copy.SetEnabled("dsh-external-tools", false, true);
                bool enabled = copy.SetEnabled("dsh-external-tools", true, true);
                result["writeTest"] = disabled && enabled;

                copy.EnterSafeMode(true);
                bool safeActive = File.Exists(copy.SafeModePath);
                copy.ExitSafeMode(true);
                bool safeRestored = !File.Exists(copy.SafeModePath);
                result["safeModeTest"] = safeActive && safeRestored;

                copy.ResetPatchLayer();
                result["patchTest"] = File.Exists(Path.Combine(copy.ProfileDir, "cordis.patch.yml"));

                result["version"] = Version;
                result["ok"] = true;
            }
            catch (Exception error)
            {
                result["ok"] = false;
                result["error"] = error.Message;
                result["version"] = Version;
            }
            finally
            {
                if (temporary != null)
                {
                    try { Directory.Delete(temporary, true); }
                    catch { }
                }
            }

            if (outPath == null)
            {
                outPath = Path.Combine(Path.GetTempPath(), "dsh-external-tools-self-test.json");
            }
            string json = ManifestStore.PrettyJson(result, 0);
            bool written = TryWrite(outPath, json);
            if (!written)
            {
                written = TryWrite(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "self-test-result.json"), json);
            }
            if (!written)
            {
                written = TryWrite(Path.Combine(Directory.GetCurrentDirectory(), "self-test-result.json"), json);
            }
            return written && (bool)result["ok"] ? 0 : 1;
        }

        private static bool TryWrite(string path, string text)
        {
            try
            {
                File.WriteAllText(path, text, new UTF8Encoding(false));
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Render the real window at its design client size. This is used to
        /// verify the layout without depending on screen DPI or a screenshot tool.
        /// </summary>
        private static int UiPreview(string[] args)
        {
            string output = args.Length > 1 ? args[1] : Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "ui-preview.png");
            int tab = 0;
            if (args.Length > 2)
            {
                int.TryParse(args[2], out tab);
            }
            int previewWidth = 1060;
            int previewHeight = 720;
            if (args.Length > 4)
            {
                int.TryParse(args[3], out previewWidth);
                int.TryParse(args[4], out previewHeight);
            }
            if (previewWidth < 800) previewWidth = 800;
            if (previewHeight < 560) previewHeight = 560;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainForm.SuppressStartupDialogs = true;
            MainForm form = new MainForm();
            form.FormBorderStyle = FormBorderStyle.None;
            form.MinimumSize = Size.Empty;
            form.ClientSize = new Size(previewWidth, previewHeight);
            form.WindowState = FormWindowState.Normal;
            form.Show();
            form.SelectModule(tab);
            Application.DoEvents();
            if (args.Length > 5 && (args[5] == "en" || args[5] == "zh"))
            {
                form.SwitchLanguageForTest(args[5]);
                Application.DoEvents();
            }
            form.Refresh();
            Application.DoEvents();
            using (Bitmap client = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            {
                form.DrawToBitmap(client, new Rectangle(0, 0, client.Width, client.Height));
                DrawWin11Window(client, output, form.Text);
            }
            form.Close();
            return 0;
        }

        private static void DrawWin11Window(Bitmap client, string output, string title)
        {
            const int pad = 20;
            const int shadow = 12;
            const int titleHeight = 44;
            int windowWidth = client.Width;
            int windowHeight = titleHeight + client.Height;
            int width = windowWidth + pad * 2 + shadow * 2;
            int height = windowHeight + pad * 2 + shadow * 2;

            using (Bitmap canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(canvas))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (LinearGradientBrush background = new LinearGradientBrush(
                    new Rectangle(0, 0, width, height),
                    Color.FromArgb(233, 239, 249),
                    Color.FromArgb(248, 250, 253),
                    90F))
                {
                    graphics.FillRectangle(background, 0, 0, width, height);
                }

                for (int index = shadow; index >= 1; index--)
                {
                    using (GraphicsPath shadowPath = Ui.Rounded(new Rectangle(pad - index, pad - index + 3,
                        windowWidth + index * 2, windowHeight + index * 2), 12 + index))
                    using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(5, 15, 23, 42)))
                    {
                        graphics.FillPath(shadowBrush, shadowPath);
                    }
                }

                Rectangle window = new Rectangle(pad, pad, windowWidth, windowHeight);
                using (GraphicsPath windowPath = Ui.Rounded(window, 10))
                {
                    graphics.SetClip(windowPath);
                    graphics.FillRectangle(Brushes.White, window);
                    using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(248, 250, 253)))
                    {
                        graphics.FillRectangle(titleBrush, new Rectangle(pad, pad, windowWidth, titleHeight));
                    }
                    graphics.DrawImage(client, pad, pad + titleHeight, windowWidth, client.Height);
                    graphics.ResetClip();
                    using (Pen border = new Pen(Color.FromArgb(214, 219, 231)))
                    {
                        graphics.DrawPath(border, windowPath);
                    }
                }

                using (Pen separator = new Pen(Color.FromArgb(231, 234, 241)))
                {
                    graphics.DrawLine(separator, pad, pad + titleHeight - 1, pad + windowWidth, pad + titleHeight - 1);
                }

                try
                {
                    using (Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                    using (Bitmap bitmap = icon.ToBitmap())
                    {
                        graphics.DrawImage(bitmap, pad + 16, pad + 12, 20, 20);
                    }
                }
                catch
                {
                }

                using (Font font = new Font(Ui.FontFamily, 9.5F, FontStyle.Regular))
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(32, 32, 32)))
                {
                    graphics.DrawString(title, font, brush, new PointF(pad + 44, pad + 12));
                }

                DrawCaptionButton(graphics, pad + windowWidth - 46, pad, 46, titleHeight, 2);
                DrawCaptionButton(graphics, pad + windowWidth - 92, pad, 46, titleHeight, 1);
                DrawCaptionButton(graphics, pad + windowWidth - 138, pad, 46, titleHeight, 0);

                canvas.Save(output, ImageFormat.Png);
            }
        }

        private static void DrawCaptionButton(Graphics graphics, int x, int y, int width, int height, int kind)
        {
            using (Pen pen = new Pen(Color.FromArgb(55, 55, 55), 1F))
            {
                int centerX = x + width / 2;
                int centerY = y + height / 2;
                if (kind == 0)
                {
                    graphics.DrawLine(pen, centerX - 5, centerY + 1, centerX + 5, centerY + 1);
                }
                else if (kind == 1)
                {
                    graphics.DrawRectangle(pen, centerX - 5, centerY - 5, 10, 10);
                }
                else
                {
                    graphics.DrawLine(pen, centerX - 5, centerY - 5, centerX + 5, centerY + 5);
                    graphics.DrawLine(pen, centerX + 5, centerY - 5, centerX - 5, centerY + 5);
                }
            }
        }

        /// <summary>
        /// Headless diagnostic report: profile checks plus the rule-based log
        /// analysis, written as JSON. This is the same data the window shows.
        /// </summary>
        private static int Diagnose(string[] args)
        {
            string profileArg = null;
            string outPath = null;
            for (int index = 1; index < args.Length; index++)
            {
                if (args[index] == "--out" && index + 1 < args.Length)
                {
                    outPath = args[++index];
                }
                else if (profileArg == null)
                {
                    profileArg = args[index];
                }
            }

            Dictionary<string, object> result = new Dictionary<string, object>();
            try
            {
                ManifestStore store = new ManifestStore(profileArg);
                List<DiagItem> items = store.Diagnose();
                List<LogFinding> findings = store.AnalyzeLogs();
                result["ok"] = true;
                result["version"] = Version;
                result["profileDir"] = store.ProfileDir;
                result["manifestPath"] = store.ManifestPath;
                result["dshRunning"] = ManifestStore.DshProcessCount() > 0;
                DshEnvironment.Refresh();
                result["dshVersion"] = DshEnvironment.InstalledVersion;
                result["dshVersionState"] = DshEnvironment.State.ToString();
                result["reinstallAllowed"] = DshEnvironment.ReinstallAllowed;
                result["webAvailable"] = DshEnvironment.WebAvailable;
                result["installer"] = DshEnvironment.FindInstaller();
                List<object> itemList = new List<object>();
                foreach (DiagItem item in items)
                {
                    Dictionary<string, object> entry = new Dictionary<string, object>();
                    entry["level"] = item.Level;
                    entry["title"] = item.Title;
                    entry["detail"] = item.Detail;
                    itemList.Add(entry);
                }
                result["items"] = itemList.ToArray();
                List<object> findingList = new List<object>();
                foreach (LogFinding finding in findings)
                {
                    Dictionary<string, object> entry = new Dictionary<string, object>();
                    entry["id"] = finding.Id;
                    entry["severity"] = finding.Severity;
                    entry["title"] = finding.Title;
                    entry["count"] = finding.Count;
                    entry["sample"] = finding.Sample;
                    entry["file"] = finding.File;
                    entry["suggestion"] = finding.Suggestion;
                    entry["action"] = finding.Action;
                    findingList.Add(entry);
                }
                result["findings"] = findingList.ToArray();
            }
            catch (Exception error)
            {
                result["ok"] = false;
                result["error"] = error.Message;
                result["version"] = Version;
            }

            string json = ManifestStore.PrettyJson(result, 0);
            if (outPath == null)
            {
                outPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "diagnose-result.json");
            }
            bool written = TryWrite(outPath, json);
            if (!written)
            {
                written = TryWrite(Path.Combine(Directory.GetCurrentDirectory(), "diagnose-result.json"), json);
            }
            return written ? 0 : 1;
        }
    }

    /// <summary>
    /// Direct reader/writer for the profile manifest. It deliberately uses only
    /// .NET Framework and never loads DSH, Node, or the plugin.
    /// </summary>
    public class ManifestStore
    {
        public static readonly string[] ProtectedNames = new string[]
        {
            "@deepseek-ai/dsh-base",
            "@deepseek-ai/dsh-web-app",
            "@deepseek-ai/dsh-sdk-app",
            "@deepseek-ai/dsh-sdk-minimal",
            "@deepseek-ai/dsh-headless",
            "@deepseek-ai/dsh-acp-app",
            "@deepseek-ai/dsh-plugin-manager"
        };

        private static readonly string[] DshProcessNames = new string[] { "DeepSeek Harness", "dsh", "dsh-desktop" };
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private const string SelfBundle = "dsh-external-tools";

        public string ProfileDir { get; private set; }
        public string ManifestPath { get; private set; }

        public ManifestStore(string explicitDir)
        {
            ProfileDir = ResolveProfile(explicitDir);
            ManifestPath = Path.Combine(ProfileDir, "package.json");
        }

        public string ToolsDir
        {
            get { return Path.Combine(ProfileDir, ".dsh-external-tools"); }
        }

        public string BackupDir
        {
            get { return Path.Combine(ToolsDir, "backups"); }
        }

        public string SafeModePath
        {
            get { return Path.Combine(ToolsDir, "safe-mode.json"); }
        }

        public static string ResolveProfile(string explicitDir)
        {
            if (!string.IsNullOrEmpty(explicitDir))
            {
                return Path.GetFullPath(explicitDir);
            }

            string saved = LoadSavedProfile();
            if (!string.IsNullOrEmpty(saved) && File.Exists(Path.Combine(saved, "package.json")))
            {
                return saved;
            }

            string environment = Environment.GetEnvironmentVariable("DSH_PROFILE_DIR");
            if (!string.IsNullOrEmpty(environment) && File.Exists(Path.Combine(environment, "package.json")))
            {
                return environment;
            }

            string home = Environment.GetEnvironmentVariable("DSH_HOME");
            if (string.IsNullOrEmpty(home))
            {
                home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh");
            }
            string profile = Environment.GetEnvironmentVariable("DSH_PROFILE");
            if (string.IsNullOrEmpty(profile))
            {
                profile = "desktop";
            }
            return Path.GetFullPath(Path.Combine(home, "profiles", profile));
        }

        private static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "dsh-external-tools",
                    "profile.txt");
            }
        }

        public static void SaveProfile(string directory)
        {
            string parent = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }
            File.WriteAllText(SettingsPath, directory, Utf8NoBom);
        }

        private static string LoadSavedProfile()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    return File.ReadAllText(SettingsPath).Trim();
                }
            }
            catch
            {
            }
            return null;
        }

        public string ReadManifestText()
        {
            if (!File.Exists(ManifestPath))
            {
                throw new FileNotFoundException("找不到 profile 配置：" + ManifestPath);
            }
            return File.ReadAllText(ManifestPath, Encoding.UTF8).TrimStart('\uFEFF');
        }

        private static Dictionary<string, object> AsDict(object value)
        {
            return value as Dictionary<string, object>;
        }

        private static object GetValue(Dictionary<string, object> dictionary, string key)
        {
            if (dictionary == null)
            {
                return null;
            }
            object value;
            return dictionary.TryGetValue(key, out value) ? value : null;
        }

        private static Dictionary<string, object> EnsureDict(Dictionary<string, object> parent, string key)
        {
            object existing = GetValue(parent, key);
            Dictionary<string, object> dictionary = existing as Dictionary<string, object>;
            if (dictionary != null)
            {
                return dictionary;
            }
            dictionary = new Dictionary<string, object>();
            parent[key] = dictionary;
            return dictionary;
        }

        public static object Parse(string text)
        {
            object parsed = Serializer.DeserializeObject(text);
            if (parsed == null)
            {
                throw new InvalidOperationException("配置为空");
            }
            return parsed;
        }

        public Dictionary<string, object> ReadDoc()
        {
            object parsed = Parse(ReadManifestText());
            Dictionary<string, object> doc = parsed as Dictionary<string, object>;
            if (doc == null)
            {
                throw new InvalidOperationException("profile 配置不是 JSON 对象");
            }
            return doc;
        }

        public List<string> GetBundles(Dictionary<string, object> doc)
        {
            List<string> result = new List<string>();
            Dictionary<string, object> dsh = AsDict(GetValue(doc, "dsh"));
            if (dsh == null)
            {
                return result;
            }
            Dictionary<string, object> profile = AsDict(GetValue(dsh, "profile"));
            if (profile == null)
            {
                return result;
            }
            object[] raw = GetValue(profile, "bundles") as object[];
            if (raw == null)
            {
                return result;
            }
            foreach (object item in raw)
            {
                string name = item as string;
                if (name != null)
                {
                    result.Add(name);
                }
            }
            return result;
        }

        public List<string> GetDependencies(Dictionary<string, object> doc)
        {
            List<string> result = new List<string>();
            Dictionary<string, object> dependencies = AsDict(GetValue(doc, "dependencies"));
            if (dependencies != null)
            {
                foreach (string key in dependencies.Keys)
                {
                    result.Add(key);
                }
            }
            return result;
        }

        public bool IsProtected(string name)
        {
            foreach (string item in ProtectedNames)
            {
                if (string.Equals(item, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsProfileInstalled(Dictionary<string, object> doc, string name)
        {
            if (GetDependencies(doc).Contains(name))
            {
                return true;
            }
            return File.Exists(Path.Combine(ProfileDir, "node_modules", name.Replace('/', Path.DirectorySeparatorChar), "package.json"));
        }

        public bool IsInstalled(Dictionary<string, object> doc, string name)
        {
            if (name.StartsWith("@deepseek-ai/", StringComparison.Ordinal))
            {
                return true;
            }
            return IsProfileInstalled(doc, name);
        }

        public string InstalledVersion(string name)
        {
            try
            {
                string path = Path.Combine(ProfileDir, "node_modules", name.Replace('/', Path.DirectorySeparatorChar), "package.json");
                if (!File.Exists(path))
                {
                    return "";
                }
                Dictionary<string, object> doc = AsDict(Parse(File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF')));
                object version = GetValue(doc, "version");
                return version as string ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>Read display metadata without changing the package.</summary>
        private void ApplyPackageMetadata(BundleInfo info, Dictionary<string, object> rootDoc)
        {
            info.DisplayName = info.Name;
            info.Description = "";
            info.PackagePath = "";
            info.DependencySpec = "";
            info.Author = "";
            info.License = "";
            info.Repository = "";

            Dictionary<string, object> dependencies = AsDict(GetValue(rootDoc, "dependencies"));
            if (dependencies != null)
            {
                object spec;
                if (dependencies.TryGetValue(info.Name, out spec) && spec is string)
                {
                    info.DependencySpec = (string)spec;
                }
            }

            string packageJson = Path.Combine(ProfileDir, "node_modules",
                info.Name.Replace('/', Path.DirectorySeparatorChar), "package.json");
            if (!File.Exists(packageJson)) return;
            info.PackagePath = Path.GetDirectoryName(packageJson);
            try
            {
                Dictionary<string, object> package = AsDict(Parse(File.ReadAllText(packageJson, Encoding.UTF8).TrimStart('\uFEFF')));
                if (package == null) return;
                info.Description = AsString(GetValue(package, "description"));
                info.License = AsString(GetValue(package, "license"));
                info.Author = NamedValue(GetValue(package, "author"), "name");
                info.Repository = NamedValue(GetValue(package, "repository"), "url");

                string display = AsString(GetValue(package, "displayName"));
                if (display == "") display = AsString(GetValue(package, "title"));
                Dictionary<string, object> dsh = AsDict(GetValue(package, "dsh"));
                if (display == "" && dsh != null)
                {
                    display = AsString(GetValue(dsh, "displayName"));
                    if (display == "") display = AsString(GetValue(dsh, "title"));
                }
                if (display == "") display = KnownDisplayName(info.Name);
                if (display == "") display = ReadmeTitle(info.PackagePath, info.Name);
                if (display != "") info.DisplayName = display;
            }
            catch
            {
                info.DisplayName = KnownDisplayName(info.Name);
            }
        }

        private static string KnownDisplayName(string name)
        {
            switch (name)
            {
                case "dsh-ai-cut": return "AI 剪辑";
                case "dsh-ai-search": return "AI 搜索";
                case "dsh-better-sidebar": return "Better Sidebar";
                case "eac-design-world": return "EAC design world";
                default: return "";
            }
        }

        private static string ReadmeTitle(string packagePath, string name)
        {
            if (string.IsNullOrEmpty(packagePath)) return "";
            string[] files = new string[] { "README.md", "README_ZH.md", "README.zh.md", "readme.md" };
            foreach (string file in files)
            {
                string path = Path.Combine(packagePath, file);
                if (!File.Exists(path)) continue;
                try
                {
                    string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                    for (int index = 0; index < Math.Min(lines.Length, 30); index++)
                    {
                        string heading = lines[index].Trim();
                        if (!heading.StartsWith("# ")) continue;
                        heading = heading.Substring(2).Trim();
                        if (heading.Length > 52 || string.Equals(heading, name, StringComparison.OrdinalIgnoreCase)) return "";
                        if (heading.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                        {
                            heading = heading.Substring(name.Length).Trim().TrimStart('·', ':', '：', '-', '—', ' ');
                        }
                        int separator = heading.LastIndexOf('·');
                        if (separator < 0) separator = heading.IndexOf('：');
                        if (separator < 0) separator = heading.IndexOf(':');
                        if (separator >= 0 && separator + 1 < heading.Length)
                        {
                            heading = heading.Substring(separator + 1).Trim();
                        }
                        return heading.Length <= 52 ? heading : "";
                    }
                }
                catch
                {
                }
            }
            return "";
        }

        private static string AsString(object value)
        {
            return value as string ?? "";
        }

        private static string NamedValue(object value, string key)
        {
            string text = value as string;
            if (!string.IsNullOrEmpty(text)) return text;
            Dictionary<string, object> dictionary = value as Dictionary<string, object>;
            if (dictionary == null) return "";
            return AsString(GetValue(dictionary, key));
        }

        public ProfileSnapshot Describe()
        {
            Dictionary<string, object> doc = ReadDoc();
            List<string> selected = GetBundles(doc);
            List<string> dependencies = GetDependencies(doc);
            List<string> names = new List<string>();
            foreach (string name in selected)
            {
                if (!names.Contains(name))
                {
                    names.Add(name);
                }
            }
            foreach (string name in dependencies)
            {
                if (!names.Contains(name))
                {
                    names.Add(name);
                }
            }

            List<BundleInfo> bundles = new List<BundleInfo>();
            foreach (string name in names)
            {
                BundleInfo info = new BundleInfo();
                info.Name = name;
                info.Enabled = selected.Contains(name);
                info.Installed = IsInstalled(doc, name);
                info.Builtin = name.StartsWith("@deepseek-ai/", StringComparison.Ordinal) && !IsProfileInstalled(doc, name);
                info.Protected = IsProtected(name);
                info.Official = name.StartsWith("@deepseek-ai/", StringComparison.Ordinal);
                info.Self = string.Equals(name, SelfBundle, StringComparison.Ordinal);
                info.Version = InstalledVersion(name);
                ApplyPackageMetadata(info, doc);
                bundles.Add(info);
            }
            bundles.Sort(delegate(BundleInfo left, BundleInfo right)
            {
                if (left.Self != right.Self) return left.Self ? -1 : 1;
                if (left.Enabled != right.Enabled) return left.Enabled ? -1 : 1;
                if (left.Official != right.Official) return left.Official ? 1 : -1;
                return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            ProfileSnapshot snapshot = new ProfileSnapshot();
            snapshot.ProfileDir = ProfileDir;
            snapshot.ManifestPath = ManifestPath;
            snapshot.ExePath = Application.ExecutablePath;
            snapshot.CliPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "bin", "dshx.cmd");
            snapshot.Enabled = selected.Contains(SelfBundle);
            snapshot.Installed = IsInstalled(doc, SelfBundle);
            snapshot.Protected = IsProtected(SelfBundle);
            snapshot.DshProcessCount = DshProcessCount();
            snapshot.DshRunning = snapshot.DshProcessCount > 0;
            snapshot.SafeMode = File.Exists(SafeModePath);
            snapshot.BackupCount = ListBackups().Count;
            snapshot.Bundles = bundles;
            return snapshot;
        }

        public bool SetEnabled(string name, bool enabled, bool force)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException("插件名不能为空");
            }
            if (!enabled && IsProtected(name))
            {
                throw new InvalidOperationException(name + " 是受保护的内置插件，不能禁用");
            }

            Dictionary<string, object> doc = ReadDoc();
            List<string> selected = GetBundles(doc);
            bool current = selected.Contains(name);
            if (current == enabled)
            {
                return false;
            }
            if (enabled && !IsInstalled(doc, name))
            {
                throw new InvalidOperationException(name + " 尚未安装，无法启用");
            }
            if (DshProcessCount() > 0 && !force)
            {
                throw new InvalidOperationException("DSH 正在运行。请先完全退出 DeepSeek Harness，或勾选“强制写入”。");
            }

            string backup = BackupManifest();
            Dictionary<string, object> dsh = EnsureDict(doc, "dsh");
            Dictionary<string, object> profile = EnsureDict(dsh, "profile");
            object[] raw = GetValue(profile, "bundles") as object[];
            List<object> next = new List<object>();
            if (raw != null)
            {
                next.AddRange(raw);
            }
            if (enabled)
            {
                bool exists = false;
                foreach (object item in next)
                {
                    if (item is string && string.Equals((string)item, name, StringComparison.Ordinal))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    next.Add(name);
                }
            }
            else
            {
                next.RemoveAll(delegate(object item)
                {
                    return item is string && string.Equals((string)item, name, StringComparison.Ordinal);
                });
            }
            profile["bundles"] = next.ToArray();
            WriteManifest(PrettyJson(doc, 0) + "\n");

            List<string> check = GetBundles(ReadDoc());
            if (check.Contains(name) != enabled)
            {
                throw new InvalidOperationException("写入后校验失败。备份：" + backup);
            }
            Log((enabled ? "enable " : "disable ") + name + " changed=true force=" + force);
            return true;
        }

        public List<BackupInfo> ListBackups()
        {
            List<BackupInfo> result = new List<BackupInfo>();
            if (!Directory.Exists(BackupDir))
            {
                return result;
            }
            string[] files;
            try
            {
                files = Directory.GetFiles(BackupDir, "package.backup-*.json");
            }
            catch
            {
                return result;
            }
            Array.Sort(files, delegate(string left, string right)
            {
                return File.GetLastWriteTime(right).CompareTo(File.GetLastWriteTime(left));
            });
            foreach (string file in files)
            {
                BackupInfo info = new BackupInfo();
                info.Name = Path.GetFileName(file);
                info.Path = file;
                info.Bytes = new FileInfo(file).Length;
                info.Modified = File.GetLastWriteTime(file);
                result.Add(info);
            }
            return result;
        }

        public string BackupManifest()
        {
            Directory.CreateDirectory(BackupDir);
            string name = "package.backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Process.GetCurrentProcess().Id + ".json";
            string target = Path.Combine(BackupDir, name);
            File.Copy(ManifestPath, target, true);
            PruneBackups();
            return target;
        }

        private void PruneBackups()
        {
            try
            {
                string[] files = Directory.GetFiles(BackupDir, "package.backup-*.json");
                Array.Sort(files, delegate(string left, string right)
                {
                    return File.GetLastWriteTime(right).CompareTo(File.GetLastWriteTime(left));
                });
                for (int index = 10; index < files.Length; index++)
                {
                    try { File.Delete(files[index]); }
                    catch { }
                }
            }
            catch
            {
            }
        }

        public void RestoreBackup(string path, bool force)
        {
            string full = Path.GetFullPath(path);
            string root = Path.GetFullPath(BackupDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("只能恢复备份目录中的文件");
            }
            if (!File.Exists(full))
            {
                throw new FileNotFoundException("备份不存在：" + full);
            }
            if (DshProcessCount() > 0 && !force)
            {
                throw new InvalidOperationException("DSH 正在运行。请先完全退出 DeepSeek Harness，或勾选“强制写入”。");
            }

            string text = File.ReadAllText(full, Encoding.UTF8).TrimStart('\uFEFF');
            Dictionary<string, object> doc = Parse(text) as Dictionary<string, object>;
            if (doc == null || GetBundles(doc) == null)
            {
                throw new InvalidOperationException("备份不是合法的 profile 配置");
            }
            BackupManifest();
            File.WriteAllText(ManifestPath, text, Utf8NoBom);
            ReadDoc();
            Log("restore " + Path.GetFileName(full));
        }

        public string EnterSafeMode(bool force)
        {
            Dictionary<string, object> doc = ReadDoc();
            List<string> selected = GetBundles(doc);
            List<string> keep = new List<string>();
            foreach (string name in selected)
            {
                if (string.Equals(name, "@deepseek-ai/dsh-base", StringComparison.Ordinal) ||
                    string.Equals(name, "@deepseek-ai/dsh-web-app", StringComparison.Ordinal))
                {
                    keep.Add(name);
                }
            }
            List<string> removed = new List<string>();
            foreach (string name in selected)
            {
                if (!keep.Contains(name))
                {
                    removed.Add(name);
                }
            }
            if (removed.Count == 0)
            {
                throw new InvalidOperationException("没有可停用的插件，当前已经是安全模式状态。");
            }
            if (DshProcessCount() > 0 && !force)
            {
                throw new InvalidOperationException("DSH 正在运行。请先结束 DSH 进程，或勾选“强制写入”。");
            }

            string backup = BackupManifest();
            SafeModeInfo info = new SafeModeInfo();
            info.CreatedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            info.Removed = removed;
            info.Previous = selected;
            Directory.CreateDirectory(ToolsDir);
            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["CreatedAt"] = info.CreatedAt;
            payload["Removed"] = info.Removed.ToArray();
            payload["Previous"] = info.Previous.ToArray();
            File.WriteAllText(SafeModePath, PrettyJson(payload, 0), Utf8NoBom);

            Dictionary<string, object> dsh = EnsureDict(doc, "dsh");
            Dictionary<string, object> profile = EnsureDict(dsh, "profile");
            profile["bundles"] = keep.ToArray();
            WriteManifest(PrettyJson(doc, 0) + "\n");
            ReadDoc();
            Log("safe-mode enter removed=" + removed.Count);
            return "已进入安全模式，停用 " + removed.Count + " 个插件。\n备份：" + backup;
        }

        public string ExitSafeMode(bool force)
        {
            if (!File.Exists(SafeModePath))
            {
                throw new InvalidOperationException("当前没有安全模式记录。");
            }
            if (DshProcessCount() > 0 && !force)
            {
                throw new InvalidOperationException("DSH 正在运行。请先结束 DSH 进程，或勾选“强制写入”。");
            }

            Dictionary<string, object> raw = AsDict(Parse(File.ReadAllText(SafeModePath, Encoding.UTF8).TrimStart('\uFEFF')));
            if (raw == null)
            {
                throw new InvalidOperationException("安全模式记录无法解析");
            }
            SafeModeInfo info = new SafeModeInfo();
            info.Removed = new List<string>();
            info.Previous = new List<string>();
            object removedValue;
            if (raw.TryGetValue("Removed", out removedValue))
            {
                object[] array = removedValue as object[];
                if (array != null)
                {
                    foreach (object item in array)
                    {
                        if (item is string) info.Removed.Add((string)item);
                    }
                }
            }

            Dictionary<string, object> doc = ReadDoc();
            List<string> selected = GetBundles(doc);
            string backup = BackupManifest();
            foreach (string name in info.Removed)
            {
                if (!selected.Contains(name) && IsInstalled(doc, name))
                {
                    selected.Add(name);
                }
            }
            Dictionary<string, object> dsh = EnsureDict(doc, "dsh");
            Dictionary<string, object> profile = EnsureDict(dsh, "profile");
            profile["bundles"] = selected.ToArray();
            WriteManifest(PrettyJson(doc, 0) + "\n");
            ReadDoc();

            string archived = Path.Combine(BackupDir, "safe-mode-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
            try { File.Move(SafeModePath, archived); }
            catch { try { File.Delete(SafeModePath); } catch { } }
            Log("safe-mode exit restored=" + info.Removed.Count);
            return "已退出安全模式，恢复 " + info.Removed.Count + " 个插件。\n备份：" + backup;
        }

        public void ResetPatchLayer()
        {
            string patch = Path.Combine(ProfileDir, "cordis.patch.yml");
            if (File.Exists(patch))
            {
                Directory.CreateDirectory(BackupDir);
                string target = Path.Combine(BackupDir, "cordis.patch-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".yml");
                File.Copy(patch, target, true);
                File.WriteAllText(patch, "[]\n", Utf8NoBom);
                Log("patch reset backup=" + target);
            }
            else
            {
                File.WriteAllText(patch, "[]\n", Utf8NoBom);
                Log("patch created");
            }
        }

        public void WriteManifest(string json)
        {
            string temporary = ManifestPath + ".dshx-" + Process.GetCurrentProcess().Id + ".tmp";
            File.WriteAllText(temporary, json, Utf8NoBom);
            try
            {
                if (File.Exists(ManifestPath))
                {
                    File.Replace(temporary, ManifestPath, null);
                }
                else
                {
                    File.Move(temporary, ManifestPath);
                }
            }
            catch
            {
                File.Copy(temporary, ManifestPath, true);
                try { File.Delete(temporary); }
                catch { }
            }
        }

        public void Log(string line)
        {
            try
            {
                Directory.CreateDirectory(ToolsDir);
                File.AppendAllText(
                    Path.Combine(ToolsDir, "operations.log"),
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " + line + "\r\n",
                    Utf8NoBom);
            }
            catch
            {
            }
        }

        public List<DiagItem> Diagnose()
        {
            List<DiagItem> items = new List<DiagItem>();
            Dictionary<string, object> doc = null;
            try
            {
                doc = ReadDoc();
                items.Add(Item("OK", "profile/package.json", "可解析：" + ManifestPath));
            }
            catch (Exception error)
            {
                items.Add(Item("ERROR", "profile/package.json", "无法解析：" + error.Message));
                return items;
            }

            List<string> selected = GetBundles(doc);
            List<string> dependencies = GetDependencies(doc);
            if (selected.Count == 0)
            {
                items.Add(Item("ERROR", "dsh.profile.bundles", "启动列表为空，DSH 可能无法启动"));
            }
            else
            {
                items.Add(Item("OK", "dsh.profile.bundles", "共 " + selected.Count + " 项"));
            }

            List<string> duplicates = new List<string>();
            List<string> seen = new List<string>();
            foreach (string name in selected)
            {
                if (seen.Contains(name))
                {
                    if (!duplicates.Contains(name)) duplicates.Add(name);
                }
                else
                {
                    seen.Add(name);
                }
            }
            if (duplicates.Count > 0)
            {
                items.Add(Item("WARN", "重复 bundle", string.Join(", ", duplicates.ToArray())));
            }

            if (!selected.Contains("@deepseek-ai/dsh-base"))
            {
                items.Add(Item("ERROR", "核心 bundle", "缺少 @deepseek-ai/dsh-base"));
            }
            if (!selected.Contains("@deepseek-ai/dsh-web-app"))
            {
                items.Add(Item("WARN", "核心 bundle", "缺少 @deepseek-ai/dsh-web-app，Web 界面可能无法加载"));
            }

            foreach (string name in selected)
            {
                if (name.StartsWith("@deepseek-ai/", StringComparison.Ordinal))
                {
                    continue;
                }
                if (!IsInstalled(doc, name))
                {
                    items.Add(Item("ERROR", "缺失插件", name + " 已选择但 node_modules 中找不到 package.json"));
                }
            }
            foreach (string name in dependencies)
            {
                if (name.StartsWith("@deepseek-ai/", StringComparison.Ordinal))
                {
                    continue;
                }
                if (!IsProfileInstalled(doc, name))
                {
                    items.Add(Item("WARN", "依赖未安装", name + " 在 dependencies 中但 node_modules 中没有"));
                }
            }

            string patch = Path.Combine(ProfileDir, "cordis.patch.yml");
            if (!File.Exists(patch))
            {
                items.Add(Item("WARN", "cordis.patch.yml", "文件不存在，用户 patch 层不会加载"));
            }
            else
            {
                try
                {
                    string text = File.ReadAllText(patch, Encoding.UTF8);
                    items.Add(Item("OK", "cordis.patch.yml", "存在（" + new FileInfo(patch).Length + " B）"));
                    foreach (string line in text.Split('\n'))
                    {
                        if (line.StartsWith("\t", StringComparison.Ordinal))
                        {
                            items.Add(Item("WARN", "cordis.patch.yml", "包含以 Tab 开头的缩进，YAML 可能解析失败"));
                            break;
                        }
                    }
                }
                catch (Exception error)
                {
                    items.Add(Item("ERROR", "cordis.patch.yml", "无法读取：" + error.Message));
                }
            }

            if (!Directory.Exists(Path.Combine(ProfileDir, "node_modules")))
            {
                items.Add(Item("ERROR", "node_modules", "目录不存在，需要安装依赖"));
            }
            else
            {
                items.Add(Item("OK", "node_modules", "存在"));
            }
            if (!File.Exists(Path.Combine(ProfileDir, "pnpm-lock.yaml")))
            {
                items.Add(Item("WARN", "pnpm-lock.yaml", "文件不存在，依赖版本可能不可复现"));
            }

            int processes = DshProcessCount();
            if (processes > 0)
            {
                items.Add(Item("INFO", "DSH 进程", processes + " 个进程正在运行；修复前请先结束"));
            }
            else
            {
                items.Add(Item("OK", "DSH 进程", "未运行"));
            }

            if (File.Exists(SafeModePath))
            {
                items.Add(Item("WARN", "安全模式", "安全模式记录存在；确认 DSH 能启动后请退出安全模式"));
            }

            List<string> logs = FindLogFiles(ProfileDir);
            List<LogFinding> findings = LogAnalyzer.Analyze(logs);
            if (logs.Count == 0)
            {
                items.Add(Item("WARN", "日志", "没有找到日志文件"));
            }
            else if (findings.Count == 0)
            {
                items.Add(Item("OK", "日志", "找到 " + logs.Count + " 个日志文件，最近内容未命中常见错误规则"));
            }
            else
            {
                int shown = 0;
                foreach (LogFinding finding in findings)
                {
                    if (shown >= 8) break;
                    string when = "";
                    try { when = " (" + File.GetLastWriteTime(finding.File).ToString("yyyy-MM-dd HH:mm") + ")"; }
                    catch { }
                    items.Add(Item(finding.Severity, "日志: " + finding.Title,
                        "出现 " + finding.Count + " 次；" + finding.Suggestion +
                        "；文件: " + Path.GetFileName(finding.File) + when +
                        "；样本: " + finding.Sample));
                    shown++;
                }
                if (findings.Count > shown)
                {
                    items.Add(Item("INFO", "日志", "另有 " + (findings.Count - shown) + " 类问题，打开日志页查看完整内容"));
                }
            }

            string dshExe = FindDshExecutable();
            if (dshExe == null)
            {
                items.Add(Item("WARN", "DSH 可执行文件", "未找到；无法从此工具启动 DSH"));
            }
            else
            {
                items.Add(Item("OK", "DSH 可执行文件", dshExe));
            }
            return items;
        }

        public List<LogFinding> AnalyzeLogs()
        {
            return LogAnalyzer.Analyze(FindLogFiles(ProfileDir));
        }

        public static DiagItem Item(string level, string title, string detail)
        {
            DiagItem item = new DiagItem();
            item.Level = level;
            item.Title = title;
            item.Detail = detail;
            return item;
        }

        public static int DshProcessCount()
        {
            int count = 0;
            foreach (string name in DshProcessNames)
            {
                try
                {
                    Process[] processes = Process.GetProcessesByName(name);
                    count += processes.Length;
                    foreach (Process process in processes)
                    {
                        process.Dispose();
                    }
                }
                catch
                {
                }
            }
            return count;
        }

        public static List<Process> DshProcesses()
        {
            List<Process> result = new List<Process>();
            foreach (string name in DshProcessNames)
            {
                try
                {
                    result.AddRange(Process.GetProcessesByName(name));
                }
                catch
                {
                }
            }
            return result;
        }

        public static string FindDshExecutable()
        {
            string environment = Environment.GetEnvironmentVariable("DSH_NODE_EXECUTABLE");
            if (!string.IsNullOrEmpty(environment) && File.Exists(environment))
            {
                return environment;
            }
            string registered = FindRegisteredDsh();
            if (registered != null)
            {
                return registered;
            }
            string install = Environment.GetEnvironmentVariable("DSH_INSTALL_DIR");
            if (!string.IsNullOrEmpty(install))
            {
                string directory = install;
                for (int depth = 0; depth < 10; depth++)
                {
                    string candidate = Path.Combine(directory, "DeepSeek Harness.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                    DirectoryInfo parent = Directory.GetParent(directory);
                    if (parent == null)
                    {
                        break;
                    }
                    directory = parent.FullName;
                }
            }
            string[] candidates = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "DeepSeek Harness", "DeepSeek Harness.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "DeepSeek Harness", "DeepSeek Harness.exe")
            };
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>Per-user installs record their location in the uninstall key.</summary>
        private static string FindRegisteredDsh()
        {
            try
            {
                string found = FindInUninstall(RegistryHive.CurrentUser, RegistryView.Default);
                if (found != null) return found;
                found = FindInUninstall(RegistryHive.LocalMachine, RegistryView.Registry64);
                if (found != null) return found;
                return FindInUninstall(RegistryHive.LocalMachine, RegistryView.Registry32);
            }
            catch
            {
                return null;
            }
        }

        private static string FindInUninstall(RegistryHive hive, RegistryView view)
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
            using (RegistryKey uninstall = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                if (uninstall == null) return null;
                foreach (string name in uninstall.GetSubKeyNames())
                {
                    using (RegistryKey app = uninstall.OpenSubKey(name))
                    {
                        if (app == null) continue;
                        string display = app.GetValue("DisplayName") as string;
                        if (display == null || display.IndexOf("DeepSeek Harness", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        string location = app.GetValue("InstallLocation") as string;
                        if (string.IsNullOrEmpty(location)) continue;
                        string candidate = Path.Combine(location, "DeepSeek Harness.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            return null;
        }

        public static List<string> FindLogFiles(string profileDir)
        {
            List<string> files = new List<string>();
            AddFile(files, Path.Combine(profileDir, "hub.log"));
            AddDirectory(files, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh", "logs"));
            AddDirectory(files, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeepSeek Harness", "logs"));
            AddDirectory(files, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeepSeek Harness", "logs"));
            files.Sort(delegate(string left, string right)
            {
                return File.GetLastWriteTime(right).CompareTo(File.GetLastWriteTime(left));
            });
            return files;
        }

        private static void AddFile(List<string> files, string path)
        {
            try
            {
                if (File.Exists(path) && !files.Contains(path))
                {
                    files.Add(path);
                }
            }
            catch
            {
            }
        }

        private static void AddDirectory(List<string> files, string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    return;
                }
                foreach (string file in Directory.GetFiles(directory, "*.log"))
                {
                    if (!files.Contains(file))
                    {
                        files.Add(file);
                    }
                }
            }
            catch
            {
            }
        }

        public static List<string> ReadTail(string path, int count)
        {
            Queue<string> lines = new Queue<string>();
            using (StreamReader reader = new StreamReader(path, Encoding.UTF8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Enqueue(line);
                    if (lines.Count > count)
                    {
                        lines.Dequeue();
                    }
                }
            }
            return new List<string>(lines);
        }

        private static string Trim(string text, int length)
        {
            if (text == null) return "";
            return text.Length <= length ? text : text.Substring(0, length) + "…";
        }

        public static string PrettyJson(object value, int level)
        {
            string pad = new string(' ', level * 2);
            string childPad = new string(' ', (level + 1) * 2);
            if (value == null)
            {
                return "null";
            }
            string text = value as string;
            if (text != null)
            {
                return JsonString(text);
            }
            if (value is bool)
            {
                return (bool)value ? "true" : "false";
            }
            if (value is int || value is long || value is double || value is float || value is decimal)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            Dictionary<string, object> dictionary = value as Dictionary<string, object>;
            if (dictionary != null)
            {
                if (dictionary.Count == 0)
                {
                    return "{}";
                }
                StringBuilder builder = new StringBuilder();
                builder.Append("{\n");
                int index = 0;
                foreach (KeyValuePair<string, object> pair in dictionary)
                {
                    builder.Append(childPad);
                    builder.Append(JsonString(pair.Key));
                    builder.Append(": ");
                    builder.Append(PrettyJson(pair.Value, level + 1));
                    index++;
                    if (index < dictionary.Count)
                    {
                        builder.Append(',');
                    }
                    builder.Append('\n');
                }
                builder.Append(pad);
                builder.Append('}');
                return builder.ToString();
            }
            object[] array = value as object[];
            if (array != null)
            {
                if (array.Length == 0)
                {
                    return "[]";
                }
                StringBuilder builder = new StringBuilder();
                builder.Append("[\n");
                for (int index = 0; index < array.Length; index++)
                {
                    builder.Append(childPad);
                    builder.Append(PrettyJson(array[index], level + 1));
                    if (index + 1 < array.Length)
                    {
                        builder.Append(',');
                    }
                    builder.Append('\n');
                }
                builder.Append(pad);
                builder.Append(']');
                return builder.ToString();
            }
            return JsonString(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        public static string JsonString(string text)
        {
            return Serializer.Serialize(text);
        }
    }
}

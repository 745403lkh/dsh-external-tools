using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DshExternalTools
{
    public enum DshVersionState
    {
        Missing,
        Matched,
        Mismatched
    }

    /// <summary>
    /// Locates the installed Desktop build, validates its version, and opens the
    /// separate Web profile or the official installer. It never downloads DSH.
    /// </summary>
    public static class DshEnvironment
    {
        public const string SupportedSeries = "0.2.2";
        public const string SupportedLabel = "0.2.2-rc.5";

        public static string Executable { get; private set; }
        public static string InstallDir { get; private set; }
        public static string CliBinPath { get; private set; }
        public static string HomeDir { get; private set; }
        public static string InstalledVersion { get; private set; }
        public static DshVersionState State { get; private set; }

        static DshEnvironment()
        {
            Refresh();
        }

        public static void Refresh()
        {
            Executable = ManifestStore.FindDshExecutable();
            HomeDir = Environment.GetEnvironmentVariable("DSH_HOME");
            if (string.IsNullOrEmpty(HomeDir))
            {
                HomeDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh");
            }

            InstallDir = Environment.GetEnvironmentVariable("DSH_INSTALL_DIR");
            if (string.IsNullOrEmpty(InstallDir) || !InstallDir.EndsWith("dsh", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(Executable))
                {
                    string root = Path.GetDirectoryName(Executable);
                    InstallDir = Path.Combine(root, "resources", "app.asar", "dsh",
                        "node_modules", "@deepseek-ai", "dsh");
                }
            }

            CliBinPath = string.IsNullOrEmpty(InstallDir) ? null : Path.Combine(InstallDir, "lib", "bin.js");
            InstalledVersion = ReadVersion(Executable);
            if (InstalledVersion == null)
            {
                State = DshVersionState.Missing;
            }
            else if (Series(InstalledVersion) == SupportedSeries)
            {
                State = DshVersionState.Matched;
            }
            else
            {
                State = DshVersionState.Mismatched;
            }
        }

        public static bool ReinstallAllowed
        {
            get { return State != DshVersionState.Mismatched; }
        }

        public static bool WebAvailable
        {
            get { return !string.IsNullOrEmpty(Executable) && !string.IsNullOrEmpty(CliBinPath); }
        }

        public static string StateText
        {
            get
            {
                if (State == DshVersionState.Matched) return "版本匹配";
                if (State == DshVersionState.Missing) return "未找到 DSH";
                return "版本不匹配";
            }
        }

        private static string ReadVersion(string executable)
        {
            if (string.IsNullOrEmpty(executable) || !File.Exists(executable)) return null;
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(executable);
                string version = info.FileVersion;
                if (string.IsNullOrEmpty(version)) version = info.ProductVersion;
                return string.IsNullOrEmpty(version) ? null : version.Trim();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>0.2.2-rc.5 -> 0.2.2; used so a matching series is not blocked.</summary>
        public static string Series(string version)
        {
            if (string.IsNullOrEmpty(version)) return "";
            string text = version.Trim().TrimStart('v', 'V');
            int dash = text.IndexOf('-');
            if (dash >= 0) text = text.Substring(0, dash);
            string[] parts = text.Split('.');
            if (parts.Length >= 3) return parts[0] + "." + parts[1] + "." + parts[2];
            return text;
        }

        /// <summary>Best-effort local installer lookup; the user can always pick one manually.</summary>
        public static string FindInstaller()
        {
            List<string> roots = new List<string>();
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop"));
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            if (!string.IsNullOrEmpty(Executable))
            {
                roots.Add(Path.GetDirectoryName(Executable));
            }

            string best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                foreach (string file in SafeFiles(root))
                {
                    string name = Path.GetFileName(file).ToLowerInvariant();
                    if (!name.EndsWith(".exe")) continue;
                    if (name.Contains("uninstall")) continue;
                    if (name == "deepseek harness.exe") continue;
                    if (!(name.Contains("harness") || name.Contains("deepseek") || name.Contains("dsh") || name == "installer.exe")) continue;
                    DateTime time = File.GetLastWriteTime(file);
                    if (time > bestTime)
                    {
                        bestTime = time;
                        best = file;
                    }
                }
            }

            // Electron's update cache normally holds the last downloaded setup.
            string updaterInstaller = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "@deepseek-aidsh-desktop-updater", "installer.exe");
            if (File.Exists(updaterInstaller))
            {
                DateTime time = File.GetLastWriteTime(updaterInstaller);
                if (time > bestTime) best = updaterInstaller;
            }
            return best;
        }

        private static IEnumerable<string> SafeFiles(string directory)
        {
            try
            {
                return Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return new string[0];
            }
        }

        public static bool LooksLikeInstaller(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            string name = Path.GetFileName(path).ToLowerInvariant();
            if (name == "deepseek harness.exe" || name.Contains("uninstall")) return false;
            return name.EndsWith(".exe");
        }

        public static void OpenInstaller(string path)
        {
            ProcessStartInfo info = new ProcessStartInfo(path);
            info.UseShellExecute = true;
            info.WorkingDirectory = Path.GetDirectoryName(path);
            Process.Start(info);
        }

        public static string WebCommandDisplay
        {
            get
            {
                if (!WebAvailable) return "未找到 DSH CLI";
                return "set \"ELECTRON_RUN_AS_NODE=1\"\r\n" +
                    "set \"DSH_HOME=" + HomeDir + "\"\r\n" +
                    "set \"DSH_INSTALL_DIR=" + InstallDir + "\"\r\n" +
                    "\"" + Executable + "\" \"" + CliBinPath + "\" web --port 0";
            }
        }

        /// <summary>Starts the isolated web profile in a visible terminal; the CLI opens the browser.</summary>
        public static void StartWeb()
        {
            if (!WebAvailable)
            {
                throw new InvalidOperationException("未找到 DSH 可执行文件或 CLI 路径。");
            }
            string script = Path.Combine(Path.GetTempPath(), "dsh-web-" + Guid.NewGuid().ToString("N") + ".cmd");
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("@echo off");
            builder.AppendLine("setlocal");
            builder.AppendLine("set \"ELECTRON_RUN_AS_NODE=1\"");
            builder.AppendLine("set \"DSH_HOME=" + HomeDir + "\"");
            builder.AppendLine("set \"DSH_INSTALL_DIR=" + InstallDir + "\"");
            builder.AppendLine("cd /d \"" + Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\"");
            builder.AppendLine("\"" + Executable + "\" \"" + CliBinPath + "\" web --port 0");
            File.WriteAllText(script, builder.ToString(), Encoding.Default);

            ProcessStartInfo info = new ProcessStartInfo("cmd.exe");
            info.Arguments = "/k call \"" + script + "\"";
            info.UseShellExecute = true;
            info.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Process.Start(info);
        }
    }
}

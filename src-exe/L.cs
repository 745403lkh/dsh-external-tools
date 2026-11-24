using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace DshExternalTools
{
    /// <summary>
    /// Small Chinese-source localization layer. Only zh/en ship; a system
    /// language outside those falls back to Chinese.
    /// </summary>
    public static partial class L
    {
        public enum LanguageMode
        {
            System,
            Chinese,
            English
        }

        private static readonly Dictionary<string, string> English = LoadEnglish();
        private static string preferencePath;

        public static LanguageMode Mode { get; private set; }
        public static string Language { get; private set; }

        public static bool IsEnglish
        {
            get { return Language == "en"; }
        }

        static L()
        {
            preferencePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "dsh-external-tools", "language.txt");
            Mode = LoadMode();
            Language = Resolve(Mode);
        }

        public static string T(string chinese)
        {
            if (string.IsNullOrEmpty(chinese)) return chinese;
            if (Language == "en")
            {
                string translated;
                if (English.TryGetValue(chinese, out translated)) return translated;
            }
            return chinese;
        }

        public static string F(string chinese, params object[] args)
        {
            return string.Format(T(chinese), args);
        }

        public static void SetMode(LanguageMode mode)
        {
            Mode = mode;
            Language = Resolve(mode);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(preferencePath));
                File.WriteAllText(preferencePath, Value(mode), new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        private static string Resolve(LanguageMode mode)
        {
            if (mode == LanguageMode.Chinese) return "zh";
            if (mode == LanguageMode.English) return "en";
            string iso = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            if (iso == "en") return "en";
            return "zh";
        }

        private static LanguageMode LoadMode()
        {
            try
            {
                if (File.Exists(preferencePath))
                {
                    string value = File.ReadAllText(preferencePath, Encoding.UTF8).Trim().ToLowerInvariant();
                    if (value == "zh") return LanguageMode.Chinese;
                    if (value == "en") return LanguageMode.English;
                }
            }
            catch
            {
            }
            return LanguageMode.System;
        }

        private static string Value(LanguageMode mode)
        {
            if (mode == LanguageMode.Chinese) return "zh";
            if (mode == LanguageMode.English) return "en";
            return "system";
        }

        private static Dictionary<string, string> LoadEnglish()
        {
            Dictionary<string, string> empty = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(EnglishBase64)) return empty;
            try
            {
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(EnglishBase64));
                Dictionary<string, string> map =
                    new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(json);
                return map ?? empty;
            }
            catch
            {
                return empty;
            }
        }
    }
}

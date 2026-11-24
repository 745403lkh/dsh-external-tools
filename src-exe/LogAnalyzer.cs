using System;
using System.Collections.Generic;

namespace DshExternalTools
{
    /// <summary>One grouped log problem with a suggested remedy.</summary>
    public class LogFinding
    {
        public string Id;
        public string Severity;
        public string Title;
        public int Count;
        public string Sample;
        public string File;
        public string Suggestion;
        public string Action;
    }

    /// <summary>
    /// Rule-based analysis of common DSH / Node / Electron failures. It is
    /// intentionally offline and conservative: every match only adds a hint,
    /// and no repair runs without the user pressing a button.
    /// </summary>
    public static class LogAnalyzer
    {
        private class Rule
        {
            public string Id;
            public string Severity;
            public string Title;
            public string[] Needles;
            public string Suggestion;
            public string Action;
        }

        private static readonly List<Rule> Rules = BuildRules();

        private static List<Rule> BuildRules()
        {
            List<Rule> rules = new List<Rule>();
            Add(rules, "module-missing", "ERROR", "依赖或插件文件缺失",
                new string[] { "ERR_MODULE_NOT_FOUND", "Cannot find module", "Cannot find package" },
                "执行 pnpm install --prefer-offline；仍失败时检查最近安装/升级的插件，必要时安全模式。", "pnpm");
            Add(rules, "package-export", "ERROR", "插件模块格式或导出不兼容",
                new string[] { "ERR_PACKAGE_PATH_NOT_EXPORTED", "ERR_UNSUPPORTED_DIR_IMPORT", "ERR_REQUIRE_ESM", "ERR_INVALID_PACKAGE_CONFIG" },
                "更新该插件或回退版本；先用安全模式确认是不是它导致。", "safe-mode");
            Add(rules, "syntax", "ERROR", "代码语法错误（常见于插件）",
                new string[] { "SyntaxError", "Unexpected token", "Unexpected end of input" },
                "进入安全模式，再逐个启用最近安装/升级的插件定位。", "safe-mode");
            Add(rules, "port", "ERROR", "端口被占用",
                new string[] { "EADDRINUSE", "address already in use" },
                "结束卡死的 DSH 进程后重开；仍有冲突时找到占用端口的进程。", "kill-dsh");
            Add(rules, "permission", "WARN", "权限不足或文件被拒绝访问",
                new string[] { "EACCES", "EPERM", "UnauthorizedAccessException", "access denied" },
                "关闭重复的 DSH 实例和占用文件的程序；检查 profile 目录权限。", "open-logs");
            Add(rules, "file-missing", "ERROR", "文件或目录不存在",
                new string[] { "ENOENT", "no such file or directory" },
                "执行依赖修复；若某个插件目录丢失，禁用该插件或恢复备份。", "pnpm");
            Add(rules, "file-busy", "WARN", "文件被其他进程占用",
                new string[] { "EBUSY", "resource busy", "being used by another process" },
                "结束 DSH 进程后重试；确认没有第二个 DSH 实例。", "kill-dsh");
            Add(rules, "yaml", "ERROR", "cordis.patch.yml 解析失败",
                new string[] { "YAMLException", "bad indentation", "duplicate key", "YAML parse" },
                "在修复页重置 cordis.patch.yml（会先备份），再手动恢复需要的配置。", "reset-patch");
            Add(rules, "json", "ERROR", "JSON 配置损坏",
                new string[] { "EJSONPARSE", "is not valid JSON", "Unexpected token } in JSON", "JSON.parse" },
                "恢复最近一次 package.json 备份；不要手工补引号。", "backup");
            Add(rules, "sqlite", "WARN", "会话数据库损坏",
                new string[] { "SQLITE_CORRUPT", "database disk image is malformed", "session file is corrupt" },
                "先备份整个 profile；再考虑移除损坏的 session/cache 文件。", "open-logs");
            Add(rules, "memory", "ERROR", "内存不足",
                new string[] { "JavaScript heap out of memory", "out of memory", "ENOMEM" },
                "关闭其他程序、减少同时运行的会话和插件；必要时重启 DSH。", "safe-mode");
            Add(rules, "network", "WARN", "网络或模型接口不可达",
                new string[] { "ECONNREFUSED", "ETIMEDOUT", "ENOTFOUND", "EAI_AGAIN", "socket hang up" },
                "检查网络、代理和 API 地址；确认模型服务可用后重试。", "open-logs");
            Add(rules, "auth", "WARN", "认证或 API Key 失效",
                new string[] { "invalid api key", "invalid_api_key", "401 Unauthorized", "403 Forbidden", "authentication failed" },
                "在 DSH 设置里重新登录或更新 API Key 后重试。", "open-logs");
            Add(rules, "rate-limit", "WARN", "请求被限流",
                new string[] { "429", "rate limit", "too many requests" },
                "稍等再试，或降低并发/请求频率。", "open-logs");
            Add(rules, "gpu", "WARN", "GPU 或图形初始化失败",
                new string[] { "GPU process", "GLES", "Vulkan", "D3D", "Failed to create shared context" },
                "更新显卡驱动；仍失败时用 DSH 的禁用硬件加速选项启动。", "open-logs");
            Add(rules, "crash", "ERROR", "进程崩溃",
                new string[] { "Segmentation fault", "FATAL ERROR", "SIGSEGV", "SIGABRT", "A JavaScript error occurred in the main process" },
                "更新/重装 DSH；若是装插件后开始，先安全模式。", "safe-mode");
            Add(rules, "plugin-load", "ERROR", "插件加载失败",
                new string[] { "failed to load plugin", "plugin failed to load", "loader error", "plugin load error" },
                "进入安全模式，再逐个启用插件定位问题来源。", "safe-mode");
            Add(rules, "native-abi", "ERROR", "原生模块 ABI 不匹配",
                new string[] { "ERR_DLOPEN_FAILED", "NODE_MODULE_VERSION", "compiled against a different Node.js version" },
                "在 profile 目录执行 pnpm rebuild / pnpm install；仍失败时重装该插件。", "pnpm");
            Add(rules, "openssl", "WARN", "Node/OpenSSL 兼容问题",
                new string[] { "digital envelope routines::unsupported", "ERR_OSSL", "error:0308010C" },
                "更新 DSH 和插件；旧插件可能依赖旧版 Node/OpenSSL。", "open-logs");
            Add(rules, "disk", "WARN", "磁盘空间不足",
                new string[] { "ENOSPC", "no space left on device" },
                "清理磁盘后重试；重点检查 profile、缓存和日志目录。", "open-logs");
            Add(rules, "handles", "WARN", "文件句柄耗尽",
                new string[] { "EMFILE", "too many open files" },
                "重启 DSH；检查是否有插件持续打开文件。", "kill-dsh");
            Add(rules, "unhandled", "ERROR", "未处理的异步异常（插件常见）",
                new string[] { "UnhandledPromiseRejection", "Unhandled rejection" },
                "进入安全模式，再逐个启用插件定位；同时更新相关插件。", "safe-mode");
            Add(rules, "type-error", "ERROR", "插件运行时类型错误",
                new string[] { "TypeError: Cannot read", "Cannot read properties of undefined", "is not a function" },
                "更新或禁用该插件；用安全模式确认。", "safe-mode");
            Add(rules, "pnpm", "ERROR", "依赖安装失败",
                new string[] { "ERR_PNPM", "pnpm install failed" },
                "检查网络/锁文件；删除 node_modules 后重新 pnpm install。", "pnpm");
            Add(rules, "pipe", "INFO", "连接或管道中断",
                new string[] { "EPIPE", "ECONNRESET" },
                "通常是重启/关闭过程中的正常日志；持续出现时再排查。", "open-logs");
            return rules;
        }

        private static void Add(List<Rule> rules, string id, string severity, string title, string[] needles, string suggestion, string action)
        {
            Rule rule = new Rule();
            rule.Id = id;
            rule.Severity = severity;
            rule.Title = title;
            rule.Needles = needles;
            rule.Suggestion = suggestion;
            rule.Action = action;
            rules.Add(rule);
        }

        public static List<LogFinding> Analyze(List<string> files)
        {
            Dictionary<string, LogFinding> findings = new Dictionary<string, LogFinding>();
            foreach (string file in files)
            {
                List<string> lines;
                try
                {
                    lines = ManifestStore.ReadTail(file, 500);
                }
                catch
                {
                    continue;
                }
                foreach (string raw in lines)
                {
                    string line = raw == null ? "" : raw.Trim();
                    if (line.Length == 0) continue;

                    Rule matchedRule = null;
                    foreach (Rule rule in Rules)
                    {
                        foreach (string needle in rule.Needles)
                        {
                            if (line.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                matchedRule = rule;
                                break;
                            }
                        }
                        if (matchedRule != null) break;
                    }

                    if (matchedRule == null)
                    {
                        if (!LooksLikeError(line)) continue;
                        matchedRule = new Rule();
                        matchedRule.Id = "generic";
                        matchedRule.Severity = "WARN";
                        matchedRule.Title = "未归类的错误行";
                        matchedRule.Suggestion = "打开完整日志查看堆栈；若是装插件后出现，先安全模式。";
                        matchedRule.Action = "open-logs";
                    }

                    LogFinding finding;
                    if (!findings.TryGetValue(matchedRule.Id, out finding))
                    {
                        finding = new LogFinding();
                        finding.Id = matchedRule.Id;
                        finding.Severity = matchedRule.Severity;
                        finding.Title = matchedRule.Title;
                        finding.Suggestion = matchedRule.Suggestion;
                        finding.Action = matchedRule.Action;
                        finding.File = file;
                        finding.Sample = Trim(line, 320);
                        findings[matchedRule.Id] = finding;
                    }
                    finding.Count++;
                }
            }

            List<LogFinding> result = new List<LogFinding>(findings.Values);
            result.Sort(delegate(LogFinding left, LogFinding right)
            {
                int leftRank = Rank(left.Severity);
                int rightRank = Rank(right.Severity);
                if (leftRank != rightRank) return rightRank.CompareTo(leftRank);
                return right.Count.CompareTo(left.Count);
            });
            return result;
        }

        private static int Rank(string severity)
        {
            if (severity == "ERROR") return 3;
            if (severity == "WARN") return 2;
            return 1;
        }

        private static bool LooksLikeError(string line)
        {
            if (line.IndexOf("WARNING: Raw diagnostics", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }
            string lower = line.ToLowerInvariant();
            return lower.StartsWith("error") ||
                   lower.Contains("\"level\":\"error\"") ||
                   lower.Contains("\"level\": \"error\"") ||
                   lower.Contains(": error") ||
                   lower.Contains("error:") ||
                   lower.Contains("failed") ||
                   lower.Contains("exception") ||
                   lower.Contains("fatal") ||
                   lower.Contains("crash") ||
                   lower.Contains("cannot find") ||
                   lower.Contains("syntaxerror") ||
                   lower.Contains("unhandled");
        }

        private static string Trim(string text, int length)
        {
            if (text == null) return "";
            return text.Length <= length ? text : text.Substring(0, length) + "…";
        }
    }
}

# 更新日志 / Changelog

## [1.0.0-beta.15] - 2026-11-24

### 中文

**修复**
- “关于”页移除与“修复工具”重复的配置目录 / 工具目录 / 日志目录 / README 按钮，避免误以为页面串到修复工具。
- “关于”正文卡片改为撑满可用高度，去掉底部大片空白。
- “关于”页说明改为独立运行、不依赖 DSH 插件。

**整理**
- 增加 GitHub 仓库结构：`CHANGELOG.md`、`.gitignore`、`docs/RELEASING.md`。
- README 增加中文/英文预览、下载入口、快速开始和仓库结构说明。
- 补充 `repository` / `homepage` / `bugs` 元数据。

### English

**Fixed**
- Removed the duplicate config/tool/log/README buttons from About, which looked like the Repair module had leaked into the page.
- The About body card now fills the available height instead of leaving a large empty area at the bottom.
- About now states that the EXE is standalone and no DSH plugin is required.

**Repository**
- Added GitHub repository scaffolding: `CHANGELOG.md`, `.gitignore`, and `docs/RELEASING.md`.
- Added Chinese/English previews, download entry, quick start, and repository layout to the README.
- Added `repository` / `homepage` / `bugs` metadata.

## [1.0.0-beta.14] - 2026-11-24

### 中文

**修复 / 图标**
- 恢复为最初的右下角蓝色齿轮图标：白底圆形徽章 + 蓝色齿轮，不再使用腹部扳手或橙色齿轮。
- 重新生成 16 / 24 / 32 / 48 / 64 / 128 / 256 ICO，并刷新资源管理器图标缓存。

**独立产品**
- DSH 插件端不再注册设置页；EXE 可以单独保存、发送和运行。

### English

**Fixed / Icon**
- Restored the original lower-right blue gear icon: white circular badge with a blue gear; removed the belly wrench and orange gear variants.
- Regenerated the 16 / 24 / 32 / 48 / 64 / 128 / 256 ICO sizes and refreshed the Explorer icon cache.

**Standalone**
- The DSH plugin no longer registers a settings page; the EXE can be copied, sent, and run on its own.

## [1.0.0-beta.13] - 2026-11-24

### 中文

**新增**
- 插件控制显示插件名称，优先读取 `displayName` / `title` / README，未知时回退包名；悬停可看包名和说明。
- 插件控制底部新增 **插件详情 / 更新插件 / 打开目录**。
- 插件详情包含名称、包名、版本、状态、类型、安装路径、依赖声明、作者、许可、仓库、描述和 README 摘要。
- 更新插件走 npm registry：`npm install <包名>@latest --save --no-audit --no-fund --package-lock=false`；更新前自动备份 profile 的 `package.json`。
- 本地 `link:` / `file:` 插件和官方内置 bundle 会明确提示不走 npm 更新。
- 第三方插件排在官方内置插件之前，便于优先看到自己安装的插件。

**修复**
- 插件控制底部按钮被提示文字遮挡的问题。
- 自检不再依赖当前 profile 是否安装本插件。

### English

**Added**
- Plugin control now shows a display name from `displayName` / `title` / README, falling back to the package name; hover shows the package id and description.
- Added **Details / Update / Open folder** actions at the bottom of plugin control.
- Details include name, package, version, status, type, install path, dependency spec, author, license, repository, description, and a README excerpt.
- Plugin updates use the npm registry: `npm install <package>@latest --save --no-audit --no-fund --package-lock=false`; the profile `package.json` is backed up first.
- Local `link:` / `file:` plugins and official built-in bundles are explicitly excluded from npm updates.
- Third-party plugins sort before official built-ins so installed plugins are easier to find.

**Fixed**
- Bottom action buttons were hidden behind the hint text.
- Self-test no longer depends on this plugin being installed in the current profile.

## [1.0.0-beta.12] and earlier

Earlier betas established the standalone EXE, the Windows 11-style UI, safe mode, backup restore, log analysis, web DSH launch, reinstall entry, bilingual switching, and the optional DSH launcher that was later removed.

# 发布步骤 / Releasing

1. 修改版本号：
   - `src-exe/Program.cs`：`Version`、`DisplayVersion`、程序集版本
   - `package.json`
   - `bin/dshx.ps1`
   - `README.md`
2. 构建：
   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File src-exe\build.ps1
   ```
3. 运行自检：
   ```powershell
   dsh-external-tools.exe --self-test <临时 profile 副本> --out self-test.json
   ```
   要求 `ok`、`writeTest`、`safeModeTest`、`patchTest` 全部为 `true`。
4. 生成预览：
   ```powershell
   dsh-external-tools.exe --ui-preview docs/preview-zh.png 0
   dsh-external-tools.exe --ui-preview docs/preview-en.png 0 en
   ```
5. 发布物：
   - 只发布 `dsh-external-tools.exe`
   - 仓库已包含 README、LICENSE、预览和 CHANGELOG，不再重复打 ZIP
6. GitHub Release：
   - tag：`v1.0.0`（与版本号一致）
   - 只上传 EXE
   - 说明写 CHANGELOG 对应条目
   - 不要上传本地 profile、日志、备份或 `.tmp-*`
7. 不提交：
   - `src-exe/bin`、`src-exe/obj`、临时截图、npm 缓存
   - 本机 profile 路径或 token

## 备注

- 最终 EXE 使用 `highestAvailable` manifest：管理员账号启动会走 UAC。
- 没有安装器、服务或后台进程；删除 EXE 即卸载。

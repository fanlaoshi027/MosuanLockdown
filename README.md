# 墨算终端管家 MosuanLockdown

Windows 10/11 Pro 学生电脑管理工具（开发中）。

## v0.1 安全预览版

- WPF 简体中文界面
- 读取 Windows 系统和当前用户信息
- 列出能够确认权限的本地标准用户
- 选择允许的软件 EXE
- 导出**不可部署**的配置预览 XML

**本版本不会修改账户、系统安全策略或 Assigned Access 配置。**

## 下载测试

进入 [Actions](../../actions/workflows/build-windows.yml)，选择最近一次成功的运行，在 Artifacts 下载 `MosuanLockdown-v0.13-win-x64`。解压后运行 EXE。

软件尚未签名，Windows 可能显示未知发布者警告。仅从本仓库的 Actions 下载，并在测试电脑运行。

## 开发

使用 .NET 10 SDK，在 Windows 上运行：

```powershell
dotnet build src/MosuanLockdown/MosuanLockdown.csproj
```

后续版本将实现正式 Assigned Access XML、严格兼容性检测、部署与恢复；这些功能目前尚未提供。

## v0.3 部署前诊断

新增只读安全检查按钮，检查 Windows 11 版本、账户选择、EXE 是否位于 Program Files 等。路径检查不是 ACL 检查；**不得依据此报告启用系统锁定**。本版本仍不会部署策略。

## v0.4 安全诊断

新增其他已启用管理员账户枚举，以及对 EXE 与父目录的常见普通用户组写入权限的保守 ACL 扫描。此检查**不是 Windows 有效权限的完整计算**，也无法证明管理员账户可实际登录。系统策略仍不会被修改。

## v0.5 检查报告

增加 TXT 诊断报告导出：设备信息、选中学生账号、播放器 SHA-256、只读检查结果和人工恢复核对清单。报告可能包含设备名和账号信息，请勿上传公开 Issue。**尚未提供部署或自动恢复。**

## v0.6 XML 结构检查

新增“检查已有 XML 文件”，对单账户、单程序预览配置的节点、路径、自动启动、GUID 引用和 StartPins JSON 做静态检查。**不是官方 XSD 验证，也不能证明系统接受配置。**

## v0.7 本地账户格式修正

依据 Microsoft Learn，Assigned Access 的 `<Account>` 应使用本地用户名格式（如 `.\\student`），而不是 SID。生成器改为本地账户名，结构检查拒绝 SID；此修正不代表官方 XSD 或实机部署已通过。

## v0.8 XML 安全读取

读取外部 XML 时禁止 DTD 和外部实体解析，限制文档大小为 1 MiB，并拒绝预览配置中的部分额外节点和属性。仍不是官方 XSD 校验，不能直接部署。

## v0.9 上级目录 ACL 诊断

只读扫描播放器 EXE 和从其所在文件夹到磁盘根目录的 ACL，发现常见普通用户组写入 Allow 权限或父目录重解析点时提示人工复核。该扫描不是有效权限计算，不能保证应用不可替换。仍无部署功能。

## v0.10 XML 回归测试

GitHub Actions 先运行独立无外部测试框架的 XML 回归测试，再发布 EXE。覆盖有效预览、SID 账户、配置关联错误、缺少自动启动、非法 StartPins、额外 App、网络 EXE 路径等。测试通过只代表本地结构规则符合预期，**不是微软官方 XSD 验证或实际部署测试**。

## v0.11 恢复准备人工核对

界面新增备用管理员、系统备份、BitLocker 和现有策略四项人工确认，检查结果写入诊断和 TXT 报告。**软件不验证这些声明的真实性，全部勾选也不代表允许部署。** 微软指出移除 Assigned Access 配置后仍可能保留部分设置，因此恢复计划不能只依赖清除配置。

## v0.12 XML 配置边界校验

对照 [微软官方 Assigned Access XSD](https://learn.microsoft.com/en-us/windows/configuration/assigned-access/xsd) 的结构说明，进一步拒绝预览配置中不支持的根节点、Profiles/Configs 子节点、应用列表额外节点、Config 额外节点及 Taskbar 额外属性，并增加 6 个负例回归测试（共 18 项）。**仍未集成官方完整 XSD 验证，测试通过不代表 Windows 接受配置。**

## v0.13 播放器学生模式配置草案

管理员界面可以选择禁用播放器右键、危险快捷键、任意文件浏览，以及是否保留播放快捷键，并导出版本化 JSON 配置草案。**播放器尚未读取或执行此 JSON，文件也不会写入受保护的系统策略目录，所有限制均未生效。** 后续需在播放器仓库实现读取、权限校验及对应行为，再由管家安全部署。

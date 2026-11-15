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

进入 [Actions](../../actions/workflows/build-windows.yml)，选择最近一次成功的运行，在 Artifacts 下载 `MosuanLockdown-v0.5-win-x64`。解压后运行 EXE。

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

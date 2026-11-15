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

进入 [Actions](../../actions/workflows/build-windows.yml)，选择最近一次成功的运行，在 Artifacts 下载 `MosuanLockdown-v0.1-win-x64`。解压后运行 EXE。

软件尚未签名，Windows 可能显示未知发布者警告。仅从本仓库的 Actions 下载，并在测试电脑运行。

## 开发

使用 .NET 10 SDK，在 Windows 上运行：

```powershell
dotnet build src/MosuanLockdown/MosuanLockdown.csproj
```

后续版本将实现正式 Assigned Access XML、严格兼容性检测、部署与恢复；这些功能目前尚未提供。

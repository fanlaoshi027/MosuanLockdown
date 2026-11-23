# 安全边界与后续验收

v0.2 仅生成 Windows 11 22H2+ Assigned Access 配置 XML；不调用部署接口。

- XML 结构检查不等于微软 XSD 全量验证，也不等于策略可成功部署。
- 当前空 StartPins 是候选方案，必须在 Windows 11 Pro 虚拟机实测。
- 不得在管理员账户上部署；必须先验证独立恢复账户。
- 选择的 EXE 必须位于学生无写入权限的目录；本版本尚未验证 ACL。
- 默认允许的 Windows 组件、文件选择对话框、快捷键、退出程序后的桌面行为需要真实测试。
- Windows 10 的 StartLayout 路径尚未实现，不能把 Windows 11 XML 直接套用到 Windows 10。
- 下一阶段必须增加完整 XSD 验证、系统版本检测、ACL 检查、策略备份及恢复方案。


## Windows 11 Enterprise integration decision (2026-11-23)

MosuanLockdown remains the **only** Windows kiosk/permission management product. Do not duplicate this functionality in MosuanPlayer2027. Keep existing WPF/.NET 10 code and v0.23 diagnostics/backup features.

- Primary kiosk target: Windows 11 Enterprise x64, Shell Launcher for a dedicated standard student SID, normal Explorer for administrators.
- App Control for Business (WDAC) is the planned enforcement mechanism, with mandatory audit mode, Code Integrity log review, tested rollback, then explicit administrator approval before enforcement.
- Preserve existing Windows Pro / Assigned Access preview code as legacy diagnostics; do not silently deploy it as Enterprise Shell Launcher.
- Student playback application: fanlaoshi027/MosuanPlayer2027. It owns internal media browsing and must never launch Explorer, default file associations or arbitrary programs in student mode.
- Integration: one eventual installer may bundle the independently built player and MosuanLockdown manager. Installation must not automatically enable kiosk policies.
- Immediate implementation sequence: read-only Shell Launcher feature/provider and Enterprise SKU preflight; backup/export; administrator/student SID checks; reversible Shell Launcher configuration; WDAC audit; resource-browser restrictions; Windows VM escape and recovery tests.
- **Current state:** architecture confirmed, no Shell Launcher/WDAC deployment implemented or validated. Existing v0.23 deployment gate stays blocked until safe recovery is implemented and tested.

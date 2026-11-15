# 安全边界与后续验收

v0.2 仅生成 Windows 11 22H2+ Assigned Access 配置 XML；不调用部署接口。

- XML 结构检查不等于微软 XSD 全量验证，也不等于策略可成功部署。
- 当前空 StartPins 是候选方案，必须在 Windows 11 Pro 虚拟机实测。
- 不得在管理员账户上部署；必须先验证独立恢复账户。
- 选择的 EXE 必须位于学生无写入权限的目录；本版本尚未验证 ACL。
- 默认允许的 Windows 组件、文件选择对话框、快捷键、退出程序后的桌面行为需要真实测试。
- Windows 10 的 StartLayout 路径尚未实现，不能把 Windows 11 XML 直接套用到 Windows 10。
- 下一阶段必须增加完整 XSD 验证、系统版本检测、ACL 检查、策略备份及恢复方案。

using System;
using System.Collections.Generic;
using System.ServiceProcess;
using Microsoft.Win32;

namespace MosuanLockdown;

/// <summary>
/// Conservative, read-only inventory. No policy deployment or service state changes.
/// </summary>
internal static class WindowsPolicyPreflight
{
    public static string ReadOnlyReport()
    {
        var notes = new List<string>();
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false);
            var edition = key?.GetValue("EditionID")?.ToString() ?? "unknown";
            var buildText = key?.GetValue("CurrentBuildNumber")?.ToString() ?? "";
            int.TryParse(buildText, out int build);
            bool pro = edition.Equals("Professional", StringComparison.OrdinalIgnoreCase) ||
                       edition.Equals("ProfessionalN", StringComparison.OrdinalIgnoreCase);
            notes.Add(pro
                ? "△ 检测到 Windows 专业版标识；不能据此证明 AppLocker 强制执行或 Assigned Access 部署能力。"
                : "⚠ 当前不是已识别的 Windows 专业版；必须人工确认对应 SKU 的功能支持。");
            notes.Add(build >= 22621
                ? "△ 当前构建号处于 Windows 11 候选 XML 生成范围，尚未验证策略部署。"
                : "⚠ 当前构建号不在本程序 Windows 11 候选 XML 生成范围，禁止尝试应用该配置。");
        }
        catch (Exception ex)
        {
            notes.Add("⚠ 读取系统 SKU 信息失败：" + ex.Message);
        }

        try
        {
            using var service = new ServiceController("AppIDSvc");
            var status = service.Status;
            notes.Add(status == ServiceControllerStatus.Running
                ? "△ Application Identity (AppIDSvc) 当前正在运行；不代表 AppLocker 策略已生效。"
                : "⚠ Application Identity (AppIDSvc) 当前未运行；AppLocker 强制执行能力需另行核实。");
        }
        catch (Exception ex)
        {
            notes.Add("⚠ 无法读取 Application Identity 服务状态：" + ex.Message);
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Enrollments", false);
            if (key is null)
                notes.Add("△ 未找到常见 MDM 注册表路径；不能证明设备没有组织管理策略。");
            else
                notes.Add("⚠ 检测到 MDM 注册表路径；是否存在有效注册和策略冲突尚未确认。");
        }
        catch (Exception ex)
        {
            notes.Add("⚠ 无法检查 MDM 注册表路径：" + ex.Message);
        }

        notes.Add("⚠ 尚未验证本机有效组策略、Assigned Access 现有配置、学生实际登录和可恢复性；禁止部署。");
        return string.Join(Environment.NewLine, notes);
    }
}

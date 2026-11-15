using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace MosuanLockdown;

internal static class WindowsEditionCheck
{
    public static string Describe(int build, string edition, string displayVersion)
    {
        var notes = new List<string>
        {
            $"系统版本检测：Build {build}，EditionID={edition}，DisplayVersion={displayVersion}。"
        };
        bool recognized = edition.Equals("Professional", StringComparison.OrdinalIgnoreCase) ||
            edition.Equals("ProfessionalN", StringComparison.OrdinalIgnoreCase) ||
            edition.StartsWith("Enterprise", StringComparison.OrdinalIgnoreCase) ||
            edition.StartsWith("Education", StringComparison.OrdinalIgnoreCase) ||
            edition.StartsWith("IoTEnterprise", StringComparison.OrdinalIgnoreCase);
        notes.Add(recognized
            ? "△ 检测到常见受支持的 Windows 版本标识；仍须核对具体功能和策略。"
            : "⚠ Windows 版本标识未被当前检查器识别；不得据此部署。");
        notes.Add(build >= 22621
            ? "△ Windows 11 22H2+ 构建号范围；允许生成候选 XML，不代表部署兼容。"
            : build >= 19041
                ? "⚠ Windows 10 或较早 Windows 11 构建：当前 XML 生成器不支持。"
                : "✗ 系统构建号过低或不可识别。");
        notes.Add("⚠ 本检查只读，不验证 Assigned Access 实际策略执行、组策略冲突或设备管理状态。");
        return string.Join(Environment.NewLine, notes);
    }

    public static string ReadOnlyReport()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false);
            if (key is null) return "⚠ 无法读取 Windows 版本注册表。";
            var buildString = key.GetValue("CurrentBuildNumber")?.ToString();
            if (!int.TryParse(buildString, out var build))
                return "⚠ 无法识别 Windows 构建号。";
            return Describe(build, key.GetValue("EditionID")?.ToString() ?? "unknown",
                key.GetValue("DisplayVersion")?.ToString() ?? "unknown");
        }
        catch (Exception ex) { return "⚠ 系统版本只读检查失败：" + ex.Message; }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;

namespace MosuanLockdown;

/// <summary>
/// Pure validation for a future student-only kiosk setup. Never writes policies.
/// An approved plan is NOT permission to deploy: DeploymentSafetyGate stays closed.
/// </summary>
internal static class StudentKioskPlan
{
    internal sealed record Input(
        string StudentSid, string AdministratorSid, string PlayerExecutable,
        bool PlayerExists, bool SnapshotVerified, bool RecoveryTested,
        bool ApplicationControlAudited, bool ApplicationControlEnforced,
        bool AdministratorRecoveryConfirmed);

    internal sealed record Assessment(bool ReadyForDeployment, IReadOnlyList<string> Blockers, string Summary);

    public static Assessment Assess(Input input)
    {
        var blockers = new List<string>();
        if (!IsAccountSid(input.StudentSid))
            blockers.Add("学生账户 SID 无效；必须使用独立的本地标准账户。");
        if (!IsAccountSid(input.AdministratorSid))
            blockers.Add("管理员账户 SID 无效。");
        if (string.Equals(input.StudentSid, input.AdministratorSid, StringComparison.OrdinalIgnoreCase))
            blockers.Add("学生和管理员不得使用同一账户。");
        if (!IsSafeExecutablePath(input.PlayerExecutable) || !input.PlayerExists)
            blockers.Add("播放器必须是已存在的本地绝对路径 EXE；不允许脚本、UNC 或附带命令行参数。");
        if (!input.SnapshotVerified)
            blockers.Add("必须先导出并验证 Shell Launcher 原始配置快照。");
        if (!input.RecoveryTested || !input.AdministratorRecoveryConfirmed)
            blockers.Add("必须实机验证独立管理员登录和恢复正常桌面。");
        if (!input.ApplicationControlAudited)
            blockers.Add("必须先完成应用控制策略审计，确认播放器和系统组件正常运行。");
        if (!input.ApplicationControlEnforced)
            blockers.Add("Shell Launcher 不能阻止启动其他程序；应用控制白名单尚未验证生效。");

        // This never authorizes deployment. The system-level safety gate remains disabled.
        return new Assessment(false, blockers,
            blockers.Count == 0
                ? "预检材料齐全；仍须人工审核和专用测试机验收。当前版本禁止部署。"
                : "未达到安全启用条件，当前版本禁止部署。");
    }

    private static bool IsAccountSid(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("S-1-5-21-", StringComparison.Ordinal))
            return false;
        try
        {
            var sid = new SecurityIdentifier(value);
            return sid.IsAccountSid();
        }
        catch (ArgumentException) { return false; }
        catch (SystemException) { return false; }
    }

    private static bool IsSafeExecutablePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('"') || value.Contains('\n') ||
            value.Contains('\r') || value.StartsWith(@"\\", StringComparison.Ordinal))
            return false;
        if (!Path.IsPathFullyQualified(value) ||
            !string.Equals(Path.GetExtension(value), ".exe", StringComparison.OrdinalIgnoreCase))
            return false;
        // Reject relative, shell pseudo-paths, and command-line suffixes.
        return value.Length >= 4 && char.IsLetter(value[0]) && value[1] == ':' &&
            (value[2] == '\\' || value[2] == '/');
    }
}

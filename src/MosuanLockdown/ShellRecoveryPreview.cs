using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MosuanLockdown;

/// <summary>
/// Offline, read-only recovery preview for an existing Shell Launcher snapshot.
/// No WMI Set* calls, registry writes, or system-policy changes.
/// </summary>
internal static class ShellRecoveryPreview
{
    internal sealed record Result(bool Valid, string Explanation, IReadOnlyList<string> Steps);

    public static Result Inspect(string folder, string? independentlyRecordedSha256 = null)
    {
        var integrity = ShellSnapshotVerifier.Verify(folder, independentlyRecordedSha256);
        if (!integrity.Consistent)
            return new Result(false, integrity.Explanation, Array.Empty<string>());

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "inventory.json")));
            var root = doc.RootElement;
            var shellState = root.GetProperty("ShellLauncherState").GetString();
            var defaultShell = root.GetProperty("DefaultShell").GetProperty("Shell").GetString();
            var custom = root.GetProperty("CustomShells");
            if (shellState is not ("Enabled" or "Disabled") ||
                string.IsNullOrWhiteSpace(defaultShell))
                return new Result(false, "快照状态不明确，不能用于恢复规划。", Array.Empty<string>());

            var sids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in custom.EnumerateArray())
            {
                var sid = entry.GetProperty("Sid").GetString();
                var shell = entry.GetProperty("Shell").GetString();
                if (string.IsNullOrWhiteSpace(sid) || string.IsNullOrWhiteSpace(shell) ||
                    !sids.Add(sid))
                    return new Result(false, "快照含缺失或重复的账户配置，拒绝生成恢复步骤。", Array.Empty<string>());
            }

            var steps = new List<string>
            {
                "1. 使用独立管理员账户登录，不要使用学生账户执行恢复。",
                "2. 在专用测试机确认此快照对应当前设备和启用限制前的时间点。",
                "3. 先关闭学生应用控制限制；核对策略来源及实际生效状态，切勿直接删除未知策略。",
                "4. 依据原始快照人工核对 Shell Launcher 默认 Shell、账户 SID 和每个返回码动作。",
                "5. 恢复原始配置后，检查管理员 Explorer 桌面和学生登录行为。",
                "6. 重启并复测，确认限制解除且播放器与系统功能正常。"
            };
            return new Result(true,
                $"只读恢复规划：原 Shell Launcher 状态 {shellState}；默认 Shell 为 {defaultShell}；" +
                $"账户覆盖项 {custom.GetArrayLength()}。未执行任何恢复。", steps);
        }
        catch (Exception ex)
        {
            return new Result(false, "恢复规划读取失败：" + ex.Message, Array.Empty<string>());
        }
    }
}

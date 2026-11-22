using System;

namespace MosuanLockdown;

/// <summary>Fail-closed deployment gate. Advisory diagnostics can never authorize deployment.</summary>
internal static class DeploymentSafetyGate
{
    internal sealed record Result(bool CanDeploy, string Headline, string Explanation);

    public static Result Evaluate(int passed, int warnings, int blocked,
        bool backupVerified, bool recoveryTested, bool policySchemaVerified,
        bool actualDeploymentTested)
    {
        if (passed < 0 || warnings < 0 || blocked < 0)
            throw new ArgumentOutOfRangeException(nameof(passed), "诊断计数不能为负数。");
        string reasons = "未完成的正式部署前提：";
        if (blocked > 0) reasons += $"阻断项 {blocked}；";
        if (warnings > 0) reasons += $"待复核警告 {warnings}；";
        if (!backupVerified) reasons += "完整策略备份及完整性核验；";
        if (!recoveryTested) reasons += "管理员实机登录与回滚演练；";
        if (!policySchemaVerified) reasons += "微软官方策略架构校验；";
        if (!actualDeploymentTested) reasons += "受控环境真实部署测试；";
        // This application intentionally has no policy deployment implementation.
        // Never change this to true based solely on UI checks or user-entered attestations.
        return new Result(false, "部署门禁：禁止部署（安全预览版）",
            reasons + "本版本没有部署入口，所有检查只读。");
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace MosuanLockdown;

/// <summary>
/// Read-only Windows 11 Enterprise / Shell Launcher detection.
/// This class MUST NOT enable Windows features or invoke WESL_UserSetting Set* methods.
/// </summary>
internal static class EnterpriseShellPreflight
{
    internal sealed record Snapshot(
        string Edition,
        int Build,
        Architecture Architecture,
        string FeatureState,
        string WmiProviderState,
        string ShellLauncherState,
        IReadOnlyList<string> Diagnostics)
    {
        public bool IsEligibleCandidate =>
            (Edition.Equals("Enterprise", StringComparison.OrdinalIgnoreCase) ||
             Edition.Equals("EnterpriseS", StringComparison.OrdinalIgnoreCase) ||
             Edition.Equals("EnterpriseSN", StringComparison.OrdinalIgnoreCase) ||
             Edition.Equals("EnterpriseN", StringComparison.OrdinalIgnoreCase)) &&
            Build >= 22000 &&
            Architecture == Architecture.X64 &&
            FeatureState.Equals("Enabled", StringComparison.OrdinalIgnoreCase) &&
            WmiProviderState == "Available" &&
            ShellLauncherState != "Unknown";

        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Windows 11 Enterprise / Shell Launcher 只读检测");
            sb.AppendLine($"EditionID: {Edition}; Build: {Build}; Architecture: {Architecture}");
            sb.AppendLine($"可选功能: {FeatureState}; WMI 提供程序: {WmiProviderState}; Shell Launcher 状态: {ShellLauncherState}");
            foreach (var item in Diagnostics) sb.AppendLine(item);
            sb.AppendLine(IsEligibleCandidate
                ? "△ 基础环境符合候选要求；账户、恢复和策略冲突尚未验证，禁止部署。"
                : "✗ 不满足可确认的候选环境，禁止部署。");
            return sb.ToString();
        }
    }

    public static Snapshot Inspect()
    {
        string edition = "unknown";
        int build = 0;
        var diagnostic = new List<string>();
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false);
            edition = key?.GetValue("EditionID")?.ToString() ?? "unknown";
            int.TryParse(key?.GetValue("CurrentBuildNumber")?.ToString(), out build);
        }
        catch (Exception ex) { diagnostic.Add("✗ 系统版本读取失败: " + ex.Message); }

        string feature = QueryFeature(diagnostic);
        string provider = "Unknown";
        string state = "Unknown";
        try
        {
            // System.Management is used only to query WMI. No InvokeMethod calls.
            var scope = new System.Management.ManagementScope(@"\\.\root\standardcimv2\embedded");
            scope.Connect();
            using var clazz = new System.Management.ManagementClass(scope,
                new System.Management.ManagementPath("WESL_UserSetting"), null);
            clazz.Get();
            provider = "Available";
            // Only WMI read-only methods are invoked.
            try
            {
                using var enabledResult = clazz.InvokeMethod("IsEnabled", null, null);
                if (enabledResult is null || Convert.ToUInt32(enabledResult["ReturnValue"]) != 0)
                    diagnostic.Add("⚠ WESL_UserSetting.IsEnabled 未能返回成功状态。");
                else
                    state = Convert.ToBoolean(enabledResult["Enabled"]) ? "Enabled" : "Disabled";
            }
            catch (Exception ex)
            {
                diagnostic.Add("⚠ 读取 Shell Launcher 启用状态失败: " + ex.Message);
            }
            diagnostic.Add("△ Shell Launcher 提供程序只读查询完成；未执行任何 Set* 操作。");
        }
        catch (Exception ex)
        {
            provider = "Unavailable";
            diagnostic.Add("✗ 无法读取 WESL_UserSetting: " + ex.Message);
        }

        if (string.Equals(edition, "Professional", StringComparison.OrdinalIgnoreCase))
            diagnostic.Add("✗ 当前为 Windows Pro；Enterprise Shell Launcher 开发路线不支持部署。");
        if (build < 22000) diagnostic.Add("✗ 需要 Windows 11。");
        if (RuntimeInformation.OSArchitecture != Architecture.X64)
            diagnostic.Add("✗ 当前目标要求 x64 架构。");
        diagnostic.Add("⚠ Shell Launcher 并非应用白名单；WDAC 审计和回滚尚未完成。");
        return new Snapshot(edition, build, RuntimeInformation.OSArchitecture,
            feature, provider, state, diagnostic);
    }

    private static string QueryFeature(List<string> diagnostic)
    {
        try
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = "dism.exe",
                Arguments = "/Online /English /Get-FeatureInfo /FeatureName:Client-EmbeddedShellLauncher",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();
            if (!proc.WaitForExit(15000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                diagnostic.Add("✗ DISM 只读检查超时。");
                return "Unknown";
            }
            if (proc.ExitCode != 0)
            {
                diagnostic.Add("✗ DISM 检查失败: " + error.Trim());
                return "Unknown";
            }
            foreach (var line in output.Split('\n'))
            {
                string normalized = line.Trim();
                if (!normalized.StartsWith("State :", StringComparison.OrdinalIgnoreCase)) continue;
                var state = normalized.Substring("State :".Length).Trim();
                return state is "Enabled" or "Disabled" or "Enable Pending" or "Disable Pending"
                    ? state : "Unknown";
            }
            diagnostic.Add("⚠ DISM 未返回可识别的功能状态。");
            return "Unknown";
        }
        catch (Exception ex)
        {
            diagnostic.Add("✗ 无法执行 DISM 只读检查: " + ex.Message);
            return "Unknown";
        }
    }
}

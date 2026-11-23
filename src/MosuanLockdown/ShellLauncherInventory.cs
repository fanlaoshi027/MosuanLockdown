using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MosuanLockdown;

/// <summary>
/// Read-only WESL state snapshot. No policy writes. It is NOT a full OS backup.
/// </summary>
internal static class ShellLauncherInventory
{
    internal sealed record Entry(string Sid, string Shell, int DefaultAction,
        int[] CustomReturnCodes, int[] CustomReturnCodesAction);
    internal sealed record DefaultShellEntry(string Shell, int DefaultAction);
    internal sealed record Inventory(
        string Product, int SchemaVersion, DateTimeOffset CapturedAtUtc,
        string Machine, string Edition, int Build, string FeatureState,
        string ProviderState, string ShellLauncherState, DefaultShellEntry DefaultShell,
        IReadOnlyList<Entry> CustomShells, IReadOnlyList<string> Warnings);

    public static (string Folder, string Sha256) Export(string destinationRoot)
    {
        if (string.IsNullOrWhiteSpace(destinationRoot))
            throw new ArgumentException("导出位置不能为空。", nameof(destinationRoot));
        var snapshot = EnterpriseShellPreflight.Inspect();
        if (snapshot.WmiProviderState != "Available" || snapshot.ShellLauncherState == "Unknown")
            throw new InvalidOperationException("Shell Launcher 状态无法完整读取，拒绝生成可能误导的配置备份。");

        var warnings = new List<string>
        {
            "这是 Shell Launcher WESL 配置的只读清单；不是完整 Windows 系统备份，也不包含可执行的自动恢复操作。",
            "尚未证明当前配置受 MDM/GPO 管理与否；在受管设备上绝不能直接覆盖策略。",
            "不包含 WDAC、AppLocker、BitLocker、GPO、MDM、Windows 系统映像或管理员恢复凭据。",
            "配置含账户 SID、计算机名及 shell 命令行。请妥善保存，勿公开分享。"
        };

        var entries = new List<Entry>();
        DefaultShellEntry defaultShell;
        try
        {
            var scope = new ManagementScope(@"\\.\root\standardcimv2\embedded");
            scope.Connect();
            using var clazz = new ManagementClass(scope,
                new ManagementPath("WESL_UserSetting"), null);
            clazz.Get();
            // GetDefaultShell is a documented read-only WMI method.
            using var result = clazz.InvokeMethod("GetDefaultShell", null, null);
            if (result is null || Convert.ToUInt32(result["ReturnValue"]) != 0)
                throw new InvalidDataException("GetDefaultShell 未返回成功状态。");
            defaultShell = new DefaultShellEntry(
                result["Shell"]?.ToString() ?? throw new InvalidDataException("默认 Shell 为空。"),
                Convert.ToInt32(result["DefaultAction"]));

            using var searcher = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT Sid, Shell, DefaultAction, CustomReturnCodes, CustomReturnCodesAction FROM WESL_UserSetting"));
            using var results = searcher.Get();
            foreach (ManagementBaseObject item in results)
            {
                var codes = ToIntArray(item["CustomReturnCodes"]);
                var actions = ToIntArray(item["CustomReturnCodesAction"]);
                if (codes.Length != actions.Length)
                    throw new InvalidDataException("已有 Shell Launcher 返回码与动作数组长度不匹配。");
                entries.Add(new Entry(
                    item["Sid"]?.ToString() ?? throw new InvalidDataException("SID 缺失。"),
                    item["Shell"]?.ToString() ?? throw new InvalidDataException("Shell 缺失。"),
                    Convert.ToInt32(item["DefaultAction"]), codes, actions));
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Shell Launcher 快照读取不完整；未创建清单。", ex);
        }

        var inventory = new Inventory("MosuanLockdown", 2, DateTimeOffset.UtcNow,
            Environment.MachineName, snapshot.Edition, snapshot.Build, snapshot.FeatureState,
            snapshot.WmiProviderState, snapshot.ShellLauncherState, defaultShell, entries, warnings);
        var json = JsonSerializer.Serialize(inventory,
            new JsonSerializerOptions { WriteIndented = true });

        // Create a uniquely named directory; never overwrite earlier snapshots.
        Directory.CreateDirectory(destinationRoot);
        string folder = Path.Combine(destinationRoot,
            "ShellLauncher-readonly-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") +
            "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "inventory.json");
        File.WriteAllText(file, json, new UTF8Encoding(false));
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
        File.WriteAllText(Path.Combine(folder, "inventory.sha256"),
            hash + "  inventory.json" + Environment.NewLine, new UTF8Encoding(false));
        return (folder, hash);
    }

    private static int[] ToIntArray(object? value)
    {
        if (value is null) return Array.Empty<int>();
        if (value is not Array array)
            throw new InvalidDataException("WMI 返回数组类型不可识别。");
        return array.Cast<object>().Select(Convert.ToInt32).ToArray();
    }
}

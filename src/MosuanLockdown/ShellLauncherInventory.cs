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
/// Export a read-only Shell Launcher inventory to a new folder. This is NOT a restorable backup.
/// No WMI Set* methods or feature modifications are permitted here.
/// </summary>
internal static class ShellLauncherInventory
{
    internal sealed record Entry(string Sid, string Shell, string DefaultAction);
    internal sealed record Inventory(
        string Product, int SchemaVersion, DateTimeOffset CapturedAtUtc,
        string Machine, string FeatureState, string ProviderState,
        IReadOnlyList<Entry> CustomShells,
        IReadOnlyList<string> Warnings);

    public static (string Folder, string Sha256) Export(string destinationRoot)
    {
        if (string.IsNullOrWhiteSpace(destinationRoot))
            throw new ArgumentException("导出位置不能为空。", nameof(destinationRoot));
        var snapshot = EnterpriseShellPreflight.Inspect();
        if (snapshot.WmiProviderState != "Available")
            throw new InvalidOperationException("WMI Shell Launcher 提供程序不可访问；拒绝生成可能误导的空备份。");

        var warnings = new List<string>
        {
            "只读清单，不是完整系统备份；不能直接导入或恢复 Shell Launcher 配置。",
            "尚未记录默认 Shell、IsEnabled、CustomReturnCodes 等完整信息；不可作为部署/回滚依据。",
            "不包含 WDAC、AppLocker、GPO、MDM、BitLocker 或 Windows 恢复映像。",
            "账户 SID 和设备名称属于敏感环境信息，请勿公开分享。"
        };
        var entries = new List<Entry>();
        try
        {
            var scope = new ManagementScope(@"\\.\root\standardcimv2\embedded");
            scope.Connect();
            using var searcher = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT Sid, Shell, DefaultAction FROM WESL_UserSetting"));
            using var results = searcher.Get();
            foreach (ManagementBaseObject item in results)
            {
                entries.Add(new Entry(item["Sid"]?.ToString() ?? "",
                    item["Shell"]?.ToString() ?? "",
                    item["DefaultAction"]?.ToString() ?? ""));
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("读取 WESL_UserSetting 实例失败，未创建清单。", ex);
        }

        var inventory = new Inventory("MosuanLockdown", 1, DateTimeOffset.UtcNow,
            Environment.MachineName, snapshot.FeatureState, snapshot.WmiProviderState,
            entries, warnings);
        var json = JsonSerializer.Serialize(inventory, new JsonSerializerOptions { WriteIndented = true });
        // Avoid silently overwriting historical inventory snapshots.
        Directory.CreateDirectory(destinationRoot);
        string folder = Path.Combine(destinationRoot,
            "ShellLauncher-readonly-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") +
            "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "inventory.json");
        File.WriteAllText(file, json, new UTF8Encoding(false));
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
        File.WriteAllText(Path.Combine(folder, "inventory.sha256"), hash + "  inventory.json" + Environment.NewLine, new UTF8Encoding(false));
        return (folder, hash);
    }
}

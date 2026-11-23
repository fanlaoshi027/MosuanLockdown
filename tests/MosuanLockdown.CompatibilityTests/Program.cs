using MosuanLockdown;
void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
var win11 = WindowsEditionCheck.Describe(22621, "Professional", "22H2");
Assert(win11.Contains("候选 XML"), "Windows 11 candidate");
var win10 = WindowsEditionCheck.Describe(19045, "Professional", "22H2");
Assert(win10.Contains("当前 XML 生成器不支持"), "Windows 10 not supported by generator");
var unknown = WindowsEditionCheck.Describe(26100, "UnrecognizedEdition", "24H2");
Assert(unknown.Contains("未被当前检查器识别"), "Unknown edition warning");
var old = WindowsEditionCheck.Describe(17763, "Professional", "1809");
Assert(old.Contains("构建号过低"), "Old build warning");
var candidate = new EnterpriseShellPreflight.Snapshot("Enterprise", 26100,
    System.Runtime.InteropServices.Architecture.X64, "Enabled", "Available", "Disabled",
    Array.Empty<string>());
Assert(candidate.IsEligibleCandidate, "Enterprise x64 enabled feature and available provider");
Assert(!(candidate with { Edition = "Professional" }).IsEligibleCandidate, "Pro excluded");
Assert(!(candidate with { FeatureState = "Disabled" }).IsEligibleCandidate, "Feature disabled");
Assert(!(candidate with { WmiProviderState = "Unavailable" }).IsEligibleCandidate, "Provider missing");
Assert(!(candidate with { Build = 19045 }).IsEligibleCandidate, "Windows 10 excluded");
Assert(!(candidate with { ShellLauncherState = "Unknown" }).IsEligibleCandidate, "Unknown state excluded");
Assert(!(candidate with { Architecture = System.Runtime.InteropServices.Architecture.Arm64 }).IsEligibleCandidate, "ARM64 excluded");

var testFolder = Path.Combine(Path.GetTempPath(), "mosuan-lockdown-snapshot-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testFolder);
try
{
    var json = System.Text.Json.JsonSerializer.Serialize(new
    {
        Product = "MosuanLockdown",
        SchemaVersion = 2,
        ShellLauncherState = "Disabled",
        DefaultShell = new { Shell = "explorer.exe", DefaultAction = 0 },
        CustomShells = Array.Empty<object>()
    });
    var inventoryPath = Path.Combine(testFolder, "inventory.json");
    File.WriteAllText(inventoryPath, json);
    var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(inventoryPath))).ToLowerInvariant();
    File.WriteAllText(Path.Combine(testFolder, "inventory.sha256"), sha + "  inventory.json");
    Assert(ShellSnapshotVerifier.Verify(testFolder, sha).Consistent, "Snapshot and external hash valid");
    Assert(!ShellSnapshotVerifier.Verify(testFolder, new string('0', 64)).Consistent, "External hash mismatch");
    File.AppendAllText(inventoryPath, " ");
    Assert(!ShellSnapshotVerifier.Verify(testFolder).Consistent, "Tampered snapshot detected");
}
finally
{
    Directory.Delete(testFolder, recursive: true);
}
Console.WriteLine("PASS: 14 compatibility / Enterprise / snapshot tests");

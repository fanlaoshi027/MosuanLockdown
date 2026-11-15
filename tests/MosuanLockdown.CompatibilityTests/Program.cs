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
Console.WriteLine("PASS: 4 compatibility classifier tests");

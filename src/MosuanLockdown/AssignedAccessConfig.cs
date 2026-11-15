using System;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;

namespace MosuanLockdown;

internal static class AssignedAccessConfig
{
    private static readonly XNamespace Base = "http://schemas.microsoft.com/AssignedAccess/2017/config";
    private static readonly XNamespace Rs5 = "http://schemas.microsoft.com/AssignedAccess/201810/config";
    private static readonly XNamespace V5 = "http://schemas.microsoft.com/AssignedAccess/2022/config";
    private static readonly Guid ProfileId = Guid.Parse("9c6b470d-63d2-43e0-9f76-60f0f130a827");

    public static XDocument GenerateWindows11(string sid, string executable)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
            throw new NotSupportedException("当前仅支持 Windows 11 22H2 及以上版本生成正式 XML。");
        if (string.IsNullOrWhiteSpace(sid) || !sid.StartsWith("S-1-", StringComparison.Ordinal))
            throw new ArgumentException("学生账户 SID 无效。", nameof(sid));
        if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable) ||
            !string.Equals(Path.GetExtension(executable), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("必须选择已存在的本地 EXE 文件。", nameof(executable));
        var fullPath = Path.GetFullPath(executable);
        if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
            throw new ArgumentException("不允许使用网络共享上的 EXE。", nameof(executable));

        // Windows 11 StartPins requires JSON; empty pins are intentional for the preview.
        var pins = JsonSerializer.Serialize(new { pinnedList = Array.Empty<object>() });
        var id = ProfileId.ToString("B").ToUpperInvariant();
        var xml = new XDocument(
            new XElement(Base + "AssignedAccessConfiguration",
                new XAttribute(XNamespace.Xmlns + "rs5", Rs5),
                new XAttribute(XNamespace.Xmlns + "v5", V5),
                new XElement(Base + "Profiles",
                    new XElement(Base + "Profile", new XAttribute("Id", id),
                        new XElement(Base + "AllAppsList",
                            new XElement(Base + "AllowedApps",
                                new XElement(Base + "App",
                                    new XAttribute("DesktopAppPath", fullPath),
                                    new XAttribute(Rs5 + "AutoLaunch", "true")))),
                        new XElement(V5 + "StartPins", new XCData(pins)),
                        new XElement(Base + "Taskbar", new XAttribute("ShowTaskbar", "false")))),
                new XElement(Base + "Configs",
                    new XElement(Base + "Config",
                        new XElement(Base + "Account", sid),
                        new XElement(Base + "DefaultProfile", new XAttribute("Id", id))))));
        ValidateStructure(xml, sid, fullPath);
        return xml;
    }

    public static void ValidateStructure(XDocument doc, string sid, string executable)
    {
        var root = doc.Root ?? throw new InvalidDataException("缺少 XML 根元素。");
        if (root.Name != Base + "AssignedAccessConfiguration") throw new InvalidDataException("根元素错误。");
        var profile = root.Element(Base + "Profiles")?.Element(Base + "Profile")
            ?? throw new InvalidDataException("缺少配置 Profile。");
        var id = (string?)profile.Attribute("Id");
        if (!Guid.TryParse(id, out _)) throw new InvalidDataException("Profile GUID 无效。");
        var app = profile.Element(Base + "AllAppsList")?.Element(Base + "AllowedApps")?.Element(Base + "App")
            ?? throw new InvalidDataException("缺少允许的程序。");
        if ((string?)app.Attribute("DesktopAppPath") != executable ||
            (string?)app.Attribute(Rs5 + "AutoLaunch") != "true")
            throw new InvalidDataException("应用配置不一致。");
        var config = root.Element(Base + "Configs")?.Element(Base + "Config")
            ?? throw new InvalidDataException("缺少账户配置。");
        if ((string?)config.Element(Base + "Account") != sid ||
            (string?)config.Element(Base + "DefaultProfile")?.Attribute("Id") != id)
            throw new InvalidDataException("账户与配置关联错误。");
        if (profile.Element(V5 + "StartPins") is null) throw new InvalidDataException("缺少 Windows 11 StartPins。");
    }
}
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace MosuanLockdown;

internal static class AssignedAccessConfig
{
    private static readonly XNamespace Base = "http://schemas.microsoft.com/AssignedAccess/2017/config";
    private static readonly XNamespace Rs5 = "http://schemas.microsoft.com/AssignedAccess/201810/config";
    private static readonly XNamespace V5 = "http://schemas.microsoft.com/AssignedAccess/2022/config";
    private static readonly Guid ProfileId = Guid.Parse("9c6b470d-63d2-43e0-9f76-60f0f130a827");

    private static bool IsValidLocalUserName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 20 || name.EndsWith(".", StringComparison.Ordinal))
            return false;
        if (name == "." || name == "..") return false;
        return !name.Any(c => char.IsControl(c) || "\\\\/\"[]:;|=,+*?<>".Contains(c));
    }

    public static XDocument GenerateWindows11(string localUserName, string executable)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
            throw new NotSupportedException("当前仅支持 Windows 11 22H2 及以上版本生成正式 XML。");
        if (!IsValidLocalUserName(localUserName))
            throw new ArgumentException("学生本地账户名无效。", nameof(localUserName));
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
                        new XElement(Base + "Account", @".\" + localUserName),
                        new XElement(Base + "DefaultProfile", new XAttribute("Id", id))))));
        ValidateStructure(xml, @".\" + localUserName, fullPath);
        ValidatePreviewFile(xml);
        return xml;
    }


    // Structural inspection only. This does NOT substitute for Microsoft's versioned XSDs.
    public static void ValidatePreviewFile(XDocument doc)
    {
        if (doc.DocumentType is not null) throw new InvalidDataException("禁止 DTD。");
        var root = doc.Root ?? throw new InvalidDataException("XML 缺少根元素。");
        if (root.Name != Base + "AssignedAccessConfiguration")
            throw new InvalidDataException("不是预期的 Assigned Access 配置。");
        var profiles = root.Element(Base + "Profiles")?.Elements(Base + "Profile").ToList()
            ?? throw new InvalidDataException("缺少 Profiles。");
        var configs = root.Element(Base + "Configs")?.Elements(Base + "Config").ToList()
            ?? throw new InvalidDataException("缺少 Configs。");
        if (root.Elements().Count() != 2 || profiles.Count != 1 || configs.Count != 1)
            throw new InvalidDataException("预览版只接受一个 Profile 和一个 Config。");
        var profile = profiles[0];
        var id = (string?)profile.Attribute("Id");
        if (!Guid.TryParse(id, out _)) throw new InvalidDataException("Profile GUID 不合法。");
        var apps = profile.Element(Base + "AllAppsList")?.Element(Base + "AllowedApps")?.Elements(Base + "App").ToList()
            ?? throw new InvalidDataException("缺少 AllowedApps。");
        if (apps.Count != 1) throw new InvalidDataException("预览版只接受一个允许的 EXE。");
        if (profile.Elements().Count() != 3 || apps[0].Attributes().Count() != 2)
            throw new InvalidDataException("发现不支持的额外配置字段。");
        var path = (string?)apps[0].Attribute("DesktopAppPath");
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            path.StartsWith(@"\\", StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("EXE 路径格式不合法。");
        if ((string?)apps[0].Attribute(Rs5 + "AutoLaunch") != "true")
            throw new InvalidDataException("未设置自动启动。");
        var config = configs[0];
        var account = (string?)config.Element(Base + "Account");
        if (string.IsNullOrWhiteSpace(account) || !account.StartsWith(@".\", StringComparison.Ordinal) ||
            !IsValidLocalUserName(account.Substring(2)))
            throw new InvalidDataException("账户必须使用 .\\本地用户名 格式，不能使用 SID。");
        if (config.Elements().Count() != 2 || (string?)config.Element(Base + "DefaultProfile")?.Attribute("Id") != id)
            throw new InvalidDataException("DefaultProfile 引用不匹配。");
        var pins = (string?)profile.Element(V5 + "StartPins");
        if (string.IsNullOrWhiteSpace(pins))
            throw new InvalidDataException("缺少 StartPins JSON。");
        using var json = JsonDocument.Parse(pins);
        if (json.RootElement.ValueKind != JsonValueKind.Object ||
            !json.RootElement.TryGetProperty("pinnedList", out var list) ||
            list.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("StartPins 必须包含 pinnedList 数组。");
        if ((string?)profile.Element(Base + "Taskbar")?.Attribute("ShowTaskbar") != "false")
            throw new InvalidDataException("预览配置应隐藏任务栏。");
    }

    public static void ValidateStructure(XDocument doc, string accountName, string executable)
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
        if ((string?)config.Element(Base + "Account") != accountName ||
            (string?)config.Element(Base + "DefaultProfile")?.Attribute("Id") != id)
            throw new InvalidDataException("账户与配置关联错误。");
        if (profile.Element(V5 + "StartPins") is null) throw new InvalidDataException("缺少 Windows 11 StartPins。");
    }
}
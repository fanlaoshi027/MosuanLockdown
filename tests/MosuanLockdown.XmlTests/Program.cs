using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MosuanLockdown;

static class Program
{
    private static readonly XNamespace B = "http://schemas.microsoft.com/AssignedAccess/2017/config";
    private static readonly XNamespace R = "http://schemas.microsoft.com/AssignedAccess/201810/config";
    private static readonly XNamespace V = "http://schemas.microsoft.com/AssignedAccess/2022/config";
    private static int passed;

    static void Main()
    {
        Accept("valid preview", Valid());
        Reject("SID account", x => x.Descendants(B + "Account").Single().Value = "S-1-5-21-100");
        Reject("missing account", x => x.Descendants(B + "Account").Single().Remove());
        Reject("mismatched profile", x => x.Descendants(B + "DefaultProfile").Single().SetAttributeValue("Id", Guid.NewGuid().ToString("B")));
        Reject("missing autolaunch", x => x.Descendants(B + "App").Single().Attribute(R + "AutoLaunch")!.Remove());
        Reject("wrong autolaunch", x => x.Descendants(B + "App").Single().SetAttributeValue(R + "AutoLaunch", "false"));
        Reject("invalid pins JSON", x => x.Descendants(V + "StartPins").Single().Value = "{not-json");
        Reject("missing pins array", x => x.Descendants(V + "StartPins").Single().Value = "{}");
        Reject("additional app", x => x.Descendants(B + "AllowedApps").Single().Add(new XElement(B + "App", new XAttribute("DesktopAppPath", @"C:\Windows\system32\cmd.exe"))));
        Reject("UNC executable", x => x.Descendants(B + "App").Single().SetAttributeValue("DesktopAppPath", @"\\server\share\player.exe"));
        Reject("unexpected profile node", x => x.Descendants(B + "Profile").Single().Add(new XElement(B + "Unknown")));
        Reject("DTD", x => x.AddFirst(new XDocumentType("AssignedAccessConfiguration", null, null, null)));
        Console.WriteLine($"PASS: {passed} XML regression cases");
    }

    private static XDocument Valid()
    {
        var id = Guid.NewGuid().ToString("B");
        return new XDocument(new XElement(B + "AssignedAccessConfiguration",
            new XElement(B + "Profiles", new XElement(B + "Profile", new XAttribute("Id", id),
                new XElement(B + "AllAppsList", new XElement(B + "AllowedApps",
                    new XElement(B + "App", new XAttribute("DesktopAppPath", @"C:\Program Files\Player\player.exe"),
                        new XAttribute(R + "AutoLaunch", "true")))),
                new XElement(V + "StartPins", "{\"pinnedList\":[]}"),
                new XElement(B + "Taskbar", new XAttribute("ShowTaskbar", "false")))),
            new XElement(B + "Configs", new XElement(B + "Config",
                new XElement(B + "Account", @".\student"),
                new XElement(B + "DefaultProfile", new XAttribute("Id", id))))));
    }

    private static void Accept(string label, XDocument xml)
    {
        AssignedAccessConfig.ValidatePreviewFile(xml);
        Console.WriteLine("PASS: " + label);
        passed++;
    }

    private static void Reject(string label, Action<XDocument> mutate)
    {
        var xml = Valid();
        mutate(xml);
        try
        {
            AssignedAccessConfig.ValidatePreviewFile(xml);
        }
        catch (InvalidDataException)
        {
            Console.WriteLine("PASS (rejected): " + label);
            passed++;
            return;
        }
        catch (System.Text.Json.JsonException)
        {
            Console.WriteLine("PASS (rejected JSON): " + label);
            passed++;
            return;
        }
        throw new Exception("INVALID CONFIG ACCEPTED: " + label);
    }
}

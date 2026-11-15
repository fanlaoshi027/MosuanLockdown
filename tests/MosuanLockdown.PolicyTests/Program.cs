using System;
using System.IO;
using System.Text.Json.Nodes;
using MosuanLockdown;

var valid = PlayerPolicyContract.CreatePreview(true, true, true, true);
PlayerPolicyContract.ValidatePreview(valid);
int passed = 1;
void Reject(string name, Action<JsonObject> edit)
{
    var obj = JsonNode.Parse(valid)!.AsObject();
    edit(obj);
    try { PlayerPolicyContract.ValidatePreview(obj.ToJsonString()); }
    catch (InvalidDataException) { Console.WriteLine("PASS: " + name); passed++; return; }
    throw new Exception("Invalid policy accepted: " + name);
}
Reject("wrong version", x => x["schemaVersion"] = 2);
Reject("wrong product", x => x["product"] = "Other");
Reject("wrong mode", x => x["mode"] = "admin");
Reject("claims enforcement", x => x["enforcementStatus"] = "enabled");
Reject("unknown root field", x => x["unexpected"] = true);
Reject("missing restrictions", x => x.Remove("restrictions"));
Reject("wrong boolean type", x => x["restrictions"]!["disableContextMenu"] = "true");
Reject("unknown restriction", x => x["restrictions"]!["unexpected"] = true);
Reject("missing restriction", x => x["restrictions"]!.AsObject().Remove("disableUnsafeShortcuts"));
try
{
    PlayerPolicyContract.ValidatePreview(valid.Replace("\"product\": \"MosuanPlayer\"", "\"product\": \"MosuanPlayer\", \"product\": \"MosuanPlayer\""));
    throw new Exception("Duplicate JSON property accepted");
}
catch (InvalidDataException) { passed++; Console.WriteLine("PASS: duplicate key"); }
Console.WriteLine($"PASS: {passed} player policy contract tests");

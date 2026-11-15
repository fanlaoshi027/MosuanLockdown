using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MosuanLockdown;

internal static class PlayerPolicyContract
{
    public static string CreatePreview(bool blockContextMenu, bool blockUnsafeKeys,
        bool denyArbitraryFiles, bool allowPlaybackKeys)
    {
        var data = new
        {
            schemaVersion = 1,
            product = "MosuanPlayer",
            mode = "student",
            restrictions = new
            {
                disableContextMenu = blockContextMenu,
                disableUnsafeShortcuts = blockUnsafeKeys,
                denyArbitraryFileBrowsing = denyArbitraryFiles,
                allowPlaybackShortcuts = allowPlaybackKeys
            },
            enforcementStatus = "not-integrated",
            note = "配置草案：播放器尚未接入，Windows 系统限制不由此 JSON 提供。"
        };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        ValidatePreview(json);
        return json;
    }

    public static void ValidatePreview(string json)
    {
        if (json.Length > 16384) throw new InvalidDataException("配置超过大小限制。");
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 8
        });
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("配置必须是对象。");
        RequireFields(root, "schemaVersion", "product", "mode", "restrictions", "enforcementStatus", "note");
        if (root.GetProperty("schemaVersion").ValueKind != JsonValueKind.Number ||
            root.GetProperty("schemaVersion").GetInt32() != 1 ||
            root.GetProperty("product").GetString() != "MosuanPlayer" ||
            root.GetProperty("mode").GetString() != "student" ||
            root.GetProperty("enforcementStatus").GetString() != "not-integrated")
            throw new InvalidDataException("配置版本、产品或预览状态不匹配。");
        if (root.GetProperty("note").ValueKind != JsonValueKind.String)
            throw new InvalidDataException("缺少预览提示。");
        var flags = root.GetProperty("restrictions");
        if (flags.ValueKind != JsonValueKind.Object) throw new InvalidDataException("限制项格式错误。");
        RequireFields(flags, "disableContextMenu", "disableUnsafeShortcuts",
            "denyArbitraryFileBrowsing", "allowPlaybackShortcuts");
        foreach (var flag in flags.EnumerateObject())
            if (flag.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new InvalidDataException("限制项必须是布尔值：" + flag.Name);
    }

    private static void RequireFields(JsonElement obj, params string[] fields)
    {
        var properties = obj.EnumerateObject().ToArray();
        if (properties.Length != fields.Length ||
            properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != fields.Length ||
            fields.Any(field => !obj.TryGetProperty(field, out _)))
            throw new InvalidDataException("配置包含缺失、重复或未知字段。");
    }
}

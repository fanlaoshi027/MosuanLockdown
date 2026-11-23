using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MosuanLockdown;

/// <summary>
/// Only verifies an exported snapshot file. Does not restore system policies.
/// A self-contained manifest is not a cryptographic signature; compare against a
/// separately recorded SHA-256 to detect a coordinated alteration of both files.
/// </summary>
internal static class ShellSnapshotVerifier
{
    internal sealed record Result(bool Consistent, string Sha256, string Explanation);

    public static Result Verify(string folder, string? expectedSha256 = null)
    {
        try
        {
            var jsonPath = Path.Combine(folder, "inventory.json");
            var hashPath = Path.Combine(folder, "inventory.sha256");
            if (!File.Exists(jsonPath) || !File.Exists(hashPath))
                return new Result(false, "", "✗ 缺少 inventory.json 或 inventory.sha256。");
            var jsonInfo = new FileInfo(jsonPath);
            if (jsonInfo.Length <= 0 || jsonInfo.Length > 1024 * 1024)
                return new Result(false, "", "✗ 快照文件大小无效。");
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(jsonPath))).ToLowerInvariant();
            var manifest = File.ReadAllText(hashPath).Trim();
            if (!Regex.IsMatch(manifest, @"^[a-fA-F0-9]{64}  inventory\.json$"))
                return new Result(false, hash, "✗ 校验清单格式不正确。");
            if (!string.Equals(manifest[..64], hash, StringComparison.OrdinalIgnoreCase))
                return new Result(false, hash, "✗ 快照与随附 SHA-256 不匹配。");
            if (!string.IsNullOrWhiteSpace(expectedSha256))
            {
                if (!Regex.IsMatch(expectedSha256, @"^[a-fA-F0-9]{64}$"))
                    return new Result(false, hash, "✗ 输入的独立哈希应为64位十六进制。");
                if (!string.Equals(expectedSha256, hash, StringComparison.OrdinalIgnoreCase))
                    return new Result(false, hash, "✗ 快照与独立记录的 SHA-256 不匹配。");
            }
            using var document = JsonDocument.Parse(File.ReadAllBytes(jsonPath));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                root.GetProperty("Product").GetString() != "MosuanLockdown" ||
                root.GetProperty("SchemaVersion").GetInt32() != 2 ||
                root.GetProperty("CustomShells").ValueKind != JsonValueKind.Array ||
                root.GetProperty("DefaultShell").ValueKind != JsonValueKind.Object ||
                string.IsNullOrWhiteSpace(root.GetProperty("ShellLauncherState").GetString()))
                return new Result(false, hash, "✗ 快照结构不符合当前只读清单协议。");
            return new Result(true, hash,
                "✓ 文件结构与随附 SHA-256 一致。" +
                (string.IsNullOrWhiteSpace(expectedSha256)
                    ? " 未输入独立哈希，不能证明文件未被整体替换。"
                    : " 已与独立记录的 SHA-256 对比。") +
                " 本工具不提供恢复或部署。");
        }
        catch (Exception ex)
        {
            return new Result(false, "", "✗ 快照验证失败：" + ex.Message);
        }
    }
}

using System;
using System.IO;
using System.Text.Json;

namespace MosuanLockdown;

/// <summary>
/// A reviewable, non-executable proposal for student-only Shell Launcher setup.
/// This does not apply policy. Administrator shell is intentionally untouched.
/// </summary>
internal static class StudentShellProposal
{
    internal sealed record Proposal(
        string Product, int SchemaVersion, string Mode, string StudentSid,
        string PlayerExecutable, string DefaultShellPolicy, string ExitBehavior,
        string ApplicationControlStatus, string DeploymentStatus);

    public static Proposal Create(string studentSid, string playerExecutable)
    {
        if (string.IsNullOrWhiteSpace(studentSid) ||
            !studentSid.StartsWith("S-1-5-21-", StringComparison.Ordinal) ||
            !IsSafeLocalExecutable(playerExecutable))
            throw new ArgumentException("学生账户或播放器路径不符合安全预览要求。");

        return new Proposal(
            "MosuanLockdown", 1, "StudentOnly", studentSid,
            playerExecutable, "PreserveExistingDefaultAndAdministratorShell",
            "RestartPlayerAfterExit", "NotConfigured",
            "PreviewOnly_NoSystemChanges");
    }

    public static string ToJson(Proposal proposal) =>
        JsonSerializer.Serialize(proposal, new JsonSerializerOptions { WriteIndented = true });

    private static bool IsSafeLocalExecutable(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length < 7 ||
            !char.IsLetter(path[0]) || path[1] != ':' || path[2] != '\\' ||
            path.StartsWith(@"\\", StringComparison.Ordinal) ||
            path.Contains('/') || path.Contains('"') ||
            path.Contains('|') || path.Contains('<') || path.Contains('>') ||
            path.Contains('?') || path.Contains('*') ||
            path.Contains('\r') || path.Contains('\n') ||
            !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
            return false;
        foreach (var segment in path.Split('\\'))
            if (segment is "." or "..") return false;
        return Path.IsPathFullyQualified(path);
    }
}

using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows;
using System.Xml.Linq;
using Microsoft.Win32;
using System.DirectoryServices.AccountManagement;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Text;

namespace MosuanLockdown;

public partial class MainWindow : Window
{
    private sealed record AccountOption(string Name, string DisplayName, string Sid);
    public MainWindow()
    {
        InitializeComponent();
        var admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        SystemInfo.Text = $"操作系统：{Environment.OSVersion.VersionString}\n当前用户：{Environment.UserName}\n管理员权限：{(admin ? "是" : "否")}\n注意：本预览版尚未验证 Assigned Access 兼容性。";
        LoadAccounts();
    }
    private void LoadAccounts()
    {
        try
        {
            using var context = new PrincipalContext(ContextType.Machine);
            using var template = new UserPrincipal(context);
            using var searcher = new PrincipalSearcher(template);
            foreach (var entry in searcher.FindAll().OfType<UserPrincipal>())
            {
                try
                {
                    if (entry.Enabled != true || entry.Sid is null) continue;
                    bool isAdmin = entry.GetAuthorizationGroups().Any(g => g.Sid?.Value == "S-1-5-32-544");
                    if (isAdmin) continue;
                    StudentAccounts.Items.Add(new AccountOption(entry.SamAccountName ?? "", $"{entry.SamAccountName}（标准用户）", entry.Sid.Value));
                }
                catch { /* Unknown privilege state: fail closed. */ }
            }
            if (StudentAccounts.Items.Count == 0) Status.Text = "未找到可确认的本地标准用户。请在 Windows 设置中创建学生标准账户。";
        }
        catch (Exception ex) { Status.Text = "无法读取本地账户：" + ex.Message; }
    }
    private void ChooseExe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Windows 程序 (*.exe)|*.exe", Title = "选择允许学生运行的软件" };
        if (dialog.ShowDialog(this) == true) ExePath.Text = dialog.FileName;
    }


    private void Diagnose_Click(object sender, RoutedEventArgs e)
    {
        var notes = new List<string>();
        bool supported = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);
        notes.Add(supported ? "✓ Windows 11 22H2+：可生成候选配置。" : "✗ 仅支持 Windows 11 22H2+ 候选配置。");
        if (StudentAccounts.SelectedItem is AccountOption account)
            notes.Add($"✓ 已选择学生标准账户：{account.Name}。");
        else notes.Add("✗ 尚未选择已确认的学生标准账户。");

        try
        {
            using var context = new PrincipalContext(ContextType.Machine);
            using var template = new UserPrincipal(context);
            using var searcher = new PrincipalSearcher(template);
            int otherAdmins = 0;
            foreach (var user in searcher.FindAll().OfType<UserPrincipal>())
            {
                try
                {
                    if (user.Enabled != true || user.Sid is null) continue;
                    if (user.Sid.Value == WindowsIdentity.GetCurrent().User?.Value) continue;
                    if (user.GetAuthorizationGroups().Any(g => g.Sid?.Value == "S-1-5-32-544")) otherAdmins++;
                }
                catch { notes.Add("⚠ 部分账户无法确认管理员权限。"); }
            }
            notes.Add(otherAdmins > 0
                ? $"△ 找到 {otherAdmins} 个其他已启用管理员账户；尚未验证其密码和登录能力。"
                : "✗ 未找到其他可确认的已启用管理员账户；禁止部署。");
        }
        catch (Exception ex) { notes.Add("✗ 管理员恢复账户检查失败：" + ex.Message); }

        if (File.Exists(ExePath.Text) && ExePath.Text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var path = Path.GetFullPath(ExePath.Text);
            if (path.StartsWith(@"\\", StringComparison.Ordinal))
                notes.Add("✗ 不允许从网络共享运行受控 EXE。");
            else
            {
                var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                bool installed = IsWithin(path, pf) || IsWithin(path, pf86);
                notes.Add(installed ? "✓ EXE 位于 Program Files 目录树。" : "⚠ EXE 不在 Program Files 下。");
                notes.Add(CheckAcl(path));
            }
        }
        else notes.Add("✗ 尚未选择有效 EXE。");
        notes.Add("⚠ 未验证策略冲突、实际学生登录、系统回滚和 Assigned Access 完整 XSD。");
        notes.Add("结论：本版本仍不允许部署；仅供诊断和 XML 导出。");
        Status.Text = string.Join(Environment.NewLine, notes);
    }

    private static string CheckAcl(string executable)
    {
        try
        {
            // Conservative audit: check both the file and its containing directory.
            // An allow ACE alone is not an effective-access calculation; never report as fully secure.
            const FileSystemRights writes = FileSystemRights.Write | FileSystemRights.Modify |
                FileSystemRights.Delete | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
            var risky = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "S-1-1-0", // Everyone
                "S-1-5-11", // Authenticated Users
                "S-1-5-32-545" // Builtin Users
            };
            foreach (var target in new[] { executable, Path.GetDirectoryName(executable)! })
            {
                FileSystemSecurity security = Directory.Exists(target)
                    ? new DirectoryInfo(target).GetAccessControl()
                    : new FileInfo(target).GetAccessControl();
                foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                {
                    if (rule.AccessControlType != AccessControlType.Allow) continue;
                    if ((rule.FileSystemRights & writes) == 0) continue;
                    if (rule.IdentityReference is SecurityIdentifier sid && risky.Contains(sid.Value))
                        return "⚠ ACL 检测到普通用户组的写入类 Allow 权限；必须人工复核有效权限。";
                }
            }
            return "△ 未发现常见普通用户组的直接写入类 Allow 权限；未覆盖个人 SID、嵌套组、拒绝规则及完整有效权限。";
        }
        catch (Exception ex) { return "✗ 无法读取文件 ACL：" + ex.Message; }
    }

    private static bool IsWithin(string file, string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return false;
        var prefix = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (StudentAccounts.SelectedItem is not AccountOption account) { MessageBox.Show(this, "请先选择学生账号。"); return; }
        if (!File.Exists(ExePath.Text) || !ExePath.Text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        { MessageBox.Show(this, "请先选择有效的 EXE 文件。"); return; }
        XDocument doc;
        try { doc = AssignedAccessConfig.GenerateWindows11(account.Sid, ExePath.Text); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "配置检查未通过"); return; }
        var save = new SaveFileDialog { FileName = "MosuanLockdown-AssignedAccess-Win11.xml", Filter = "XML 文件|*.xml" };
        if (save.ShowDialog(this) != true) return;
        doc.Save(save.FileName);
        Status.Text = "Windows 11 Assigned Access XML 已导出。尚未进行完整 XSD 或系统部署验证，请勿直接应用。";
    }
}
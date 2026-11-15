using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows;
using System.Xml.Linq;
using System.Xml;
using Microsoft.Win32;
using System.DirectoryServices.AccountManagement;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;

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




    private void ExportPlayerPolicy_Click(object sender, RoutedEventArgs e)
    {
        // Contract draft only. Do not write to ProgramData or claim that the player enforces it.
        string json;
        try
        {
            json = PlayerPolicyContract.CreatePreview(
                BlockContextMenu.IsChecked == true,
                BlockUnsafeKeys.IsChecked == true,
                BlockArbitraryFiles.IsChecked == true,
                AllowPlaybackKeys.IsChecked == true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "配置验证失败");
            return;
        }
        var dialog = new SaveFileDialog
        {
            FileName = "MosuanPlayer-student-policy.preview.json",
            Filter = "JSON 配置文件|*.json"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName,
                json,
                new UTF8Encoding(false));
            Status.Text = "✓ 已导出播放器配置草案。尚未部署或生效，不能作为学生限制的证明。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "导出失败");
        }
    }

    private string RecoveryChecklist()
    {
        var items = new (System.Windows.Controls.CheckBox Box, string Label)[]
        {
            (RecoveryAdmin, "备用管理员登录"),
            (RecoveryBackup, "系统备份及恢复介质"),
            (RecoveryBitLocker, "BitLocker 恢复准备"),
            (RecoveryPolicies, "现有策略记录")
        };
        int count = items.Count(item => item.Box.IsChecked == true);
        var details = string.Join(Environment.NewLine,
            items.Select(item => (item.Box.IsChecked == true ? "[人工确认] " : "[未确认] ") + item.Label));
        return $"恢复准备人工核对：{count}/{items.Length} 项。"
            + Environment.NewLine + details
            + Environment.NewLine + "注意：勾选不等于验证成功，本版本禁止部署。";
    }

    private void CheckRecovery_Click(object sender, RoutedEventArgs e)
    {
        Status.Text = RecoveryChecklist();
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
        notes.Add(RecoveryChecklist());
        notes.Add("⚠ 未验证策略冲突、实际学生登录、系统回滚和 Assigned Access 完整 XSD。");
        notes.Add("结论：本版本仍不允许部署；仅供诊断和 XML 导出。");
        Status.Text = string.Join(Environment.NewLine, notes);
    }

    private static string CheckAcl(string executable)
    {
        try
        {
            // Conservative scan of the EXE and every ancestor directory to the volume root.
            // This is not an effective-access evaluation; individual SIDs and nested groups remain unverified.
            const FileSystemRights writes = FileSystemRights.Write | FileSystemRights.Modify |
                FileSystemRights.Delete | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership |
                FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories | FileSystemRights.DeleteSubdirectoriesAndFiles;
            var risky = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "S-1-1-0", "S-1-5-11", "S-1-5-32-545"
            };
            var targets = new List<string> { executable };
            var directory = new DirectoryInfo(Path.GetDirectoryName(executable)!);
            int levels = 0;
            while (directory is not null)
            {
                if (++levels > 64) return "✗ 目录层级异常，无法完成 ACL 检查。";
                targets.Add(directory.FullName);
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    return "⚠ 发现目录重解析点，可能存在符号链接或联接，需人工确认实际路径。";
                directory = directory.Parent;
            }
            foreach (var target in targets)
            {
                FileSystemSecurity security = Directory.Exists(target)
                    ? new DirectoryInfo(target).GetAccessControl()
                    : new FileInfo(target).GetAccessControl();
                foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                {
                    if (rule.AccessControlType != AccessControlType.Allow) continue;
                    if ((rule.FileSystemRights & writes) == 0) continue;
                    if (rule.IdentityReference is SecurityIdentifier sid && risky.Contains(sid.Value))
                        return "⚠ 路径 " + target + " 存在常见普通用户组写入类 Allow 权限，需人工复核。";
                }
            }
            return "△ 已扫描 EXE 和 " + (targets.Count - 1) +
                " 级父目录，未发现常见普通用户组写入类 Allow；仍未验证个人 SID、嵌套组、重解析点文件或有效权限。";
        }
        catch (Exception ex) { return "✗ 无法读取文件 ACL：" + ex.Message; }
    }

    private static bool IsWithin(string file, string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return false;
        var prefix = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }


    private void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        Diagnose_Click(sender, e);
        var report = new StringBuilder();
        report.AppendLine("墨算终端管家 v0.14 · 部署前只读检查报告");
        report.AppendLine("生成时间：" + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        report.AppendLine("机器名称：" + Environment.MachineName);
        report.AppendLine("操作系统：" + Environment.OSVersion.VersionString);
        report.AppendLine("学生账户：" + (StudentAccounts.SelectedItem is AccountOption a ? a.Name : "未选择"));
        report.AppendLine("播放器路径：" + (File.Exists(ExePath.Text) ? ExePath.Text : "未选择"));
        if (File.Exists(ExePath.Text))
        {
            try
            {
                using var stream = File.OpenRead(ExePath.Text);
                report.AppendLine("播放器 SHA-256：" + Convert.ToHexString(SHA256.HashData(stream)));
            }
            catch (Exception ex) { report.AppendLine("播放器哈希计算失败：" + ex.Message); }
        }
        report.AppendLine();
        report.AppendLine(Status.Text);
        report.AppendLine();
        report.AppendLine("恢复准备（以下勾选状态仅为人工声明）：");
        report.AppendLine(RecoveryChecklist());
        report.AppendLine("[ ] 已在真实登录界面验证备用管理员账号及密码");
        report.AppendLine("[ ] 已准备 BitLocker 恢复密钥（如启用）");
        report.AppendLine("[ ] 已创建系统还原或完整备份并确认可用");
        report.AppendLine("[ ] 已记录当前 Assigned Access / MDM / GPO 策略");
        report.AppendLine("[ ] 已在非生产测试机验证部署、退出、重启与恢复");
        report.AppendLine();
        report.AppendLine("警告：此报告不证明可安全部署；本版本不提供部署功能。");
        var dialog = new SaveFileDialog { FileName = "MosuanLockdown-safety-report.txt", Filter = "文本报告|*.txt" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, report.ToString(), new UTF8Encoding(true));
            Status.Text = "只读安全检查报告已保存。报告中包含设备与账号信息，请勿公开上传。";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "保存失败"); }
    }


    private void ValidateXml_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "XML 配置文件|*.xml", Title = "选择要检查的 Assigned Access XML" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            using var reader = XmlReader.Create(dialog.FileName, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 });
            var doc = XDocument.Load(reader, LoadOptions.None);
            AssignedAccessConfig.ValidatePreviewFile(doc);
            Status.Text = "✓ XML 预览结构检查通过：本地账户格式、应用、自动启动、配置关联和 StartPins JSON 均存在且形式正确。"
                + Environment.NewLine + "⚠ 未执行微软官方 XSD 校验，也未验证 Windows 实际接受该配置；禁止直接部署。";
        }
        catch (Exception ex)
        {
            Status.Text = "✗ XML 预览结构检查未通过：" + ex.Message;
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (StudentAccounts.SelectedItem is not AccountOption account) { MessageBox.Show(this, "请先选择学生账号。"); return; }
        if (!File.Exists(ExePath.Text) || !ExePath.Text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        { MessageBox.Show(this, "请先选择有效的 EXE 文件。"); return; }
        XDocument doc;
        try { doc = AssignedAccessConfig.GenerateWindows11(account.Name, ExePath.Text); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "配置检查未通过"); return; }
        var save = new SaveFileDialog { FileName = "MosuanLockdown-AssignedAccess-Win11.xml", Filter = "XML 文件|*.xml" };
        if (save.ShowDialog(this) != true) return;
        doc.Save(save.FileName);
        Status.Text = "Windows 11 Assigned Access XML 已导出。尚未进行完整 XSD 或系统部署验证，请勿直接应用。";
    }
}
using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows;
using System.Xml.Linq;
using Microsoft.Win32;
using System.DirectoryServices.AccountManagement;
using System.Collections.Generic;

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
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
            notes.Add("✓ Windows 11 22H2+：允许生成候选 XML（不代表可部署）。");
        else notes.Add("✗ 当前系统不支持此版本的 XML 生成。");
        if (StudentAccounts.SelectedItem is AccountOption account)
            notes.Add($"✓ 已选择学生标准账户：{account.Name}。");
        else notes.Add("✗ 未选择已确认的学生标准账户。");
        if (File.Exists(ExePath.Text) && ExePath.Text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var path = Path.GetFullPath(ExePath.Text);
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            bool installed = IsWithin(path, pf) || IsWithin(path, pf86);
            notes.Add(installed
                ? "△ EXE 位于 Program Files 下；仍需检查实际文件夹 ACL。"
                : "⚠ EXE 不在 Program Files 下，可能位于学生可写目录，不能安全部署。");
        }
        else notes.Add("✗ 尚未选择有效 EXE。");
        notes.Add("⚠ 尚未验证独立管理员恢复账户、文件 ACL、策略冲突及实际退出行为。");
        notes.Add("安全结论：仅允许导出供检查的 XML；禁止部署。");
        Status.Text = string.Join(Environment.NewLine, notes);
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
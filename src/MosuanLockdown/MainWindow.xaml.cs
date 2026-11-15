using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows;
using System.Xml.Linq;
using Microsoft.Win32;

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
            using var users = new System.DirectoryServices.AccountManagement.PrincipalContext(System.DirectoryServices.AccountManagement.ContextType.Machine);
            using var template = new System.DirectoryServices.AccountManagement.UserPrincipal(users);
            using var searcher = new System.DirectoryServices.AccountManagement.PrincipalSearcher(template);
            foreach (var entry in searcher.FindAll().OfType<System.DirectoryServices.AccountManagement.UserPrincipal>())
            {
                try
                {
                    if (entry.Enabled != true || entry.Sid is null) continue;
                    var sid = entry.Sid.Value;
                    using var identity = new WindowsIdentity(entry.Sid.ToString());
                    if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) continue;
                    StudentAccounts.Items.Add(new AccountOption(entry.SamAccountName ?? "", $"{entry.SamAccountName}（标准用户）", sid));
                }
                catch { /* Unresolvable accounts are omitted, never guessed. */ }
            }
        }
        catch (Exception ex) { Status.Text = "无法读取本地账户：" + ex.Message; }
    }

    private void ChooseExe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Windows 程序 (*.exe)|*.exe", Title = "选择允许学生运行的软件" };
        if (dialog.ShowDialog(this) == true) ExePath.Text = dialog.FileName;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (StudentAccounts.SelectedItem is not AccountOption account) { MessageBox.Show(this, "请先选择学生账号。"); return; }
        if (!File.Exists(ExePath.Text) || !ExePath.Text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        { MessageBox.Show(this, "请先选择有效的 EXE 文件。"); return; }
        var ns = XNamespace.Get("http://schemas.microsoft.com/AssignedAccess/2017/config");
        var doc = new XDocument(new XComment("仅供预览：不是可直接部署的 Assigned Access 完整配置。"),
            new XElement("MosuanLockdownPreview",
                new XElement("StudentAccountSid", account.Sid),
                new XElement("AllowedExecutable", ExePath.Text),
                new XElement("AutoLaunch", "true"),
                new XElement("TargetSchemaNamespace", ns.NamespaceName)));
        var save = new SaveFileDialog { FileName = "MosuanLockdown-preview.xml", Filter = "XML 文件|*.xml" };
        if (save.ShowDialog(this) != true) return;
        doc.Save(save.FileName);
        Status.Text = "配置预览已导出（不可直接用于部署）。";
    }
}
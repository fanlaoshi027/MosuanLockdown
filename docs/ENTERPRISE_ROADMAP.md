# MosuanLockdown: Windows 11 Enterprise roadmap

Branch: feature/enterprise-shell-wdac. Main remains the stable v0.23 safe preview.

## Scope and ownership
- MosuanLockdown is the only Windows kiosk and permission management program; preserve WPF/.NET 10 UI and previous diagnostics.
- Target: Windows 11 Enterprise x64; Windows Pro / Assigned Access stays as legacy preview only.
- MosuanPlayer2027 remains a separate player repository. Do not duplicate this manager inside the player.
- A future joint installer can package both applications but MUST NOT silently enable kiosk policies.

## Non-negotiable safety
- Never target the current administrator, recovery administrator, or an administrator group for a student shell.
- Before enabling Shell Launcher, prove an independent administrator account can sign in and restore Explorer.
- Shell Launcher changes only the shell; it cannot prevent arbitrary process execution by itself. App Control for Business (WDAC) is a separate enforcement layer.
- Take inventory and protected backups before changes. Document a tested rollback before enabling.
- Never apply WDAC enforcement without audit and explicit operator approval. Keep the current DeploymentSafetyGate hard-blocked.
- Unknown permissions, unavailable providers, managed-device policy, or unclear recovery route must be treated as BLOCKED.
- Do not save credentials or BitLocker recovery keys to logs or exported reports.

## Milestones
### E1 — Read-only Shell Launcher / Enterprise inspection
- Read edition, build, architecture.
- Query optional feature Client-EmbeddedShellLauncher using DISM without enabling it.
- Query WMI namespace root\\standardcimv2\\embedded and WESL_UserSetting without Set* invocation.
- Inspect IsEnabled and GetDefaultShell where supported; report unavailable vs unknown.
- Verify student SID, local standard user, executable path, parent ACLs, and separate admin.
- Acceptance: cannot modify shell, user accounts, or app control settings; test decision logic with mocks.

### E2 — Backup / inventory
- Snapshot all readable WESL_UserSetting configurations (including per-SID instances), provider state and feature state.
- Keep existing AppLocker XML backup and hashes; list active App Control policies without changing them.
- Export JSON inventory plus SHA-256 manifest, timestamp and explicit limitations.
- Acceptance: repeatable read-only snapshot, never overwrites previous backups, no secrets.

### E3 — Shell Launcher reversible configuration
- Validate account SID, backup hashes, admin recovery, ownership by GPO/MDM and trusted EXE path.
- Generate strict preview and controlled configuration for a dedicated student SID.
- Keep Explorer as default shell for other users/admins. Specify shell restart/exit behavior.
- Add explicit enable, verify, disable and restore operations behind a new reviewed gate.
- Acceptance: only a dedicated test device can enable, with tested admin recovery and audit log.

### E4 — WDAC audit
- Create carefully scoped candidate policy; audit CodeIntegrity/Operational events and legitimate player behavior, codecs, updates, drivers and SMB.
- Document policy servicing/rollback and administrator allowances.

### E5 — WDAC enforcement / escape testing
- Only after audit approval and sacrificial hardware tests.
- Test reboot, Win+R, Win+E, Task Manager, Ctrl+Alt+Del, file pickers, URL handlers, external programs, removable media, admin recovery, offline recovery, and system updates.

## Immediate sequence
1. Implement independent E1 report service and tests.
2. Implement E2 snapshot + hash.
3. Wire new buttons into existing WPF UI only after isolated tests.
4. Do not compile or release for every tiny edit; one CI build per substantial completed milestone.

## Microsoft references
- https://learn.microsoft.com/en-us/windows/configuration/shell-launcher/
- https://learn.microsoft.com/en-us/windows/configuration/shell-launcher/configure-wmi
- https://learn.microsoft.com/en-us/windows/configuration/shell-launcher/wesl-usersetting
- https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/deployment/audit-appcontrol-policies

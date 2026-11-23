# MosuanLockdown and MosuanPlayer2027 integration

This window and this development branch modify MosuanLockdown only.

- MosuanPlayer2027 owns video playback, playlists, SMB, internal browsing, cropping/inversion and application-level student-mode restrictions.
- MosuanLockdown owns Windows edition checks, account verification, Shell Launcher, WDAC policy lifecycle, backups and recovery.
- Do not copy security management logic into the player. Shell Launcher is not an app allowlist.
- Existing player policy draft protocol (schemaVersion 1) remains a preview, not enforcement. Future deployment requires a versioned and integrity-checked administrator-controlled configuration.
- Future single installer: install both programs but leave kiosk/WDAC disabled by default. Require explicit setup, backup, separate recovery administrator and test.

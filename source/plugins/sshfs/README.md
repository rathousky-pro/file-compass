# SSHFS support

File Compass manages SSHFS profiles using SSHFS-Win and WinFsp. This directory does not contain installers or another copy of the system driver.

If dependencies are missing, SSHFS support offers Download and install; installation never starts automatically at application startup. The pinned official versions are WinFsp 2.1.25156 and SSHFS-Win 3.5.20357 x64, verified with SHA-256 and Authenticode. Installation requests elevation where necessary, displays progress, supports cancellation/rollback and never restarts Windows automatically. Already installed dependencies are not reinstalled.

The drive is mounted under the normal Windows account for use in File Compass and other applications. Runtime lookup checks a complete `plugins/sshfs/bin/sshfs.exe` runtime, then SSHFS-Win installations under Program Files (including x86). Copying DLLs alone cannot replace installing the WinFsp driver.

- https://github.com/winfsp/sshfs-win
- https://github.com/winfsp/winfsp

Credentials are passed over stdin. Known-host records contain public server keys only and connections require StrictHostKeyChecking. Local profiles do not modify the user's `.ssh/config` or `known_hosts`.

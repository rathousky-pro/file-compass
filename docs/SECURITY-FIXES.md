# Security audit fixes

The October 2026 audit findings are addressed as follows:

| Finding | Change |
| --- | --- |
| NET-01 | FTP MLSD/LIST, SCP find/ls and SFTP directory entries reject path components before normalization. Download names receive Windows filename validation and a destination containment check. Recursive copy preparation validates original server names. |
| ARC-01 | Extraction propagates the source archive's Zone.Identifier, including partial extraction and nested archive roots. Local staging copies preserve it. Executables, scripts, shortcuts and macro-enabled documents require explicit confirmation before external opening from an archive. Remote editor and prepared preview files receive Internet zone information. |
| NET-02 | SSH profiles preserve their host pin. First use requires explicit trust; subsequent launches probe and verify the pinned public key before authentication, then require strict OpenSSH verification against a dedicated known_hosts file. User/global SSH configuration and PATH executable discovery are disabled. Native arguments use explicit Windows quoting through ProcessStartInfo, including in Windows PowerShell 5. |
| IPC-01 | The installer pipe requires the first instance and rejects remote clients. The UI verifies the launched helper PID; the helper verifies the server PID supplied by its parent before accessing packages. |
| EXP-01 | Development config/data and publisher OAuth configuration are kept in the ignored local-profile directory outside bin. Fresh portable launches no longer import old AppData credentials automatically. Release builds require an empty destination and reject private state in their output. The SDK project also excludes publisher OAuth JSON from copied cloud resources. |

Build a clean distribution using:

```powershell
./source/build.ps1 -Release
```

The script prints the fresh output directory under `dist`. Distribute that directory. A running application can recreate old profile files or lock its executable; close it before rebuilding the development `bin` directory. Profile migration preserves conflicting old profiles in recovery directories without overwriting the migrated profile. Recovery copies under `local-profile` contain personal data and must not be distributed.

Revoking or rotating credentials already exposed elsewhere is a separate provider-side operation. This change does not rotate the Google OAuth client secret, revoke cloud tokens, or change server credentials.

Regression coverage includes malicious FTP listing parsers, rejected download paths, ADS propagation, source metadata preservation, host-pin changes and persistence, actual PowerShell/native argument handling, copy operations, and first-instance pipe creation. The security test also contains real FTP and helper/worker IPC scenarios. Restricted execution environments can prevent loopback connections and named-pipe connections; these scenarios are explicitly reported as skipped rather than counted as passed.

## Medium and Low–Medium follow-up

| Finding | Implementation |
| --- | --- |
| CRY-01 | settings.xml, archives.xml, connections.xml and cloud accounts.xml retain readable XML and receive HMAC-authenticated SHA-256 seals stored with the private integrity key. A signed registry records the one-time legacy import, so removing a seal cannot reenable import. Pending digests make file/seal commits recoverable, and a per-profile named mutex serializes writers across instances. Tampering is rejected before loading or saving. |
| PLG-01 | Compiled hashes cover smartctl, optional bundled SSHFS executables and DLLs, managed libraries, and preview scripts. Runtime bootstrap verifies and locks dependencies before entering the application. Native tools remain locked through launch; external tools require Authenticode verification or an explicit saved hash approval. Automatic PATH discovery of archivers is removed. Unsigned SSHFS packages require explicit consent; changed hashes invalidate approval. |
| IPC-02 | Instance pipes require the first instance. Both peers verify the executable and Windows session, and clients use Identification impersonation level. Forwarded UNC/mapped network folders require consent before the main window accesses them. |
| CRY-02 | The config-password helper accepts only exact cloud key locations and a verified rclone parent. Config passwords use password-command rather than environment variables. The long-running SFTP bridge uses a separate client key and authorized_keys instead of a password. Google publisher identity is persisted and verified in encrypted configuration using a short-lived local rc command before starting the bridge; the bridge receives no publisher secret in its environment. Same-user DPAPI access is not a process security boundary. |
| PRV-01 | Worksheet cells use textContent instead of generated HTML. DOCX symbol values are validated and rendered as text; stylesheet contents use textContent. The bundled SheetJS parser is version 0.20.3, verified by executing the library and an XLSX write/read round trip. Its bytes match the original Git version and the preview manifest. The earlier claim that 0.18.5 was bundled and spreadsheet previews were disabled was incorrect. The viewer accepts 0.20.3 and rejects unexpected versions before XLSX.read. |
| SHL-01 | F4 opens SVG/SVGZ as text. An associated Open fallback for marked or executable documents requires confirmation. |
| FS-01 | Ancestors are checked for reparse points. Destructive recursion holds native ancestor handles without delete sharing, deletes links themselves without following them, and removes ordinary directories non-recursively after their children. Archive temporary directories validate their owned parent and use the same guarded cleanup. |
| FS-02 | Config/data roots and sensitive working subdirectories receive explicit current-user/SYSTEM ACLs. Migration secures destination directories instead of relying on the former unreachable ACL-copy branch. NTFS security semantics are required. |
| EXP-02 | Successful reauthorization removes obsolete account backups. Closing a remote edit cleans its private copy; a modified local file requires confirmation before discarding it. Files locked by external applications may remain in the private edits directory rather than being forcibly removed. SSHFS log handles no longer allow other writers. |
| NET-03 | SSH pins require a named algorithm and SHA-256 digest (or a complete public key). Existing algorithm-plus-SHA256 values are normalized; MD5 and algorithm-free generic pins are rejected. Legacy rclone RSA pins are migrated with their known algorithm. Keyboard-interactive prompts with echo enabled receive no password. |
| SHL-02 | FileMetadata, FileViewers and FilePanel native arguments use the common Windows argument quoting routine, including trailing backslashes. |
| CRY-03 | libcurl-owned application buffers record their allocation lengths and are overwritten before FreeHGlobal. Managed temporary password/key arrays are cleared; SSHFS process environment blocks are also overwritten before release. NativeSsh already used cleared managed buffers instead of the obsolete HGlobal pattern described in the audit. Allocations owned internally by third-party libraries remain their responsibility. |
| NET-04 | An open persisted port is no longer treated as proof of a running bridge. Each new tracked rclone process gets a fresh port, and transport options are updated before opening connections. |

The restricted environment also refused to start the unmodified bundled `rclone.exe version` with a Windows loader error. End-to-end cloud authorization, refresh, and SFTP bridge tests therefore still need a normal Windows session. No live accounts were used for testing or credential rotation.

## Low findings

| Finding | Implementation |
| --- | --- |
| PRV-02 | CSV text fields beginning with formula or control characters receive an apostrophe prefix, with CSV quote escaping preserved. Analyzer indexes use a current-user/SYSTEM private data directory, exclusive creation, and a writer that denies other writes and deletion. Live readers retain the sharing needed for progressive snapshots. Cleanup closes the writer before deleting an index after failure. The previous writer already denied other writers; the audit overstated that part. |
| CRY-04 | FileIntegrity state signatures already bind the file name and state-envelope purpose; a substitution regression test confirms rejection under another name. New network and proxy passwords use an explicit dpapi-v3 format and dedicated entropy, with no decryption fallback. Authenticated network settings load/save boundaries migrate unversioned passwords from both old formats and atomically persist them. Null-entropy support is restricted to this legacy migration; ordinary Unprotect rejects unversioned records. |
| BLD-01 | Release builds exclude all *Tests.cs files and all test dispatch/configuration-redirect branches using APP_TESTS. Development builds retain tests; the SDK project enables them only in Debug. Release test commands are explicitly rejected before profile initialization, while real worker/helper commands remain available. The native build passes linkArguments to the compiler instead of the unrelated args array. |

Validation: LowAuditTests passed 20 checks; IntegrityTests passed 41, DiskAnalyzerUpgradeTests 28, and MediumAuditTests 31. The release executable built successfully, inspected test types were absent, and a test command exited 2 without creating profile state. A compiler capture fixture verified the native script passes all 22 expected linker arguments; this does not substitute for rebuilding native dependencies. NetworkTests fails on its existing menu-structure assertion in both the previous medium build and the new development build, with identical failure output.

Use `source/build.ps1 -Release` for distribution and `source/build.ps1` for a development build with tests. Password migration preserves the secret values, but older application versions cannot decrypt the new versioned password format.

## Settings saves and executable approvals

Saving language or appearance no longer reads or rewrites the executable approval list for unchanged archiver paths. File existence and approval are checked only for paths edited or explicitly selected in the dialog. A replaced executable at an unchanged path is not silently approved. Invalid approval data still blocks executable acquisition; it does not block unrelated language changes. Regression coverage reproduces an invalid trusted-tools.xml, saves and applies a new language, checks that approval bytes remain untouched, rejects modified executables and confirms explicit new-path approval.

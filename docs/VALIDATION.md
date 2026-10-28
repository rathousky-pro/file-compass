# Validation — 28 October 2026

The maintained source builds as File Compass 0.9.5 beta. The installed development candidate is build `20261028.90`; a separate clean Release build `20261028.91` also compiled successfully. This is source-publication validation, not a public binary release or a claim that every historical test was rerun.

## Content checks

- Eight interface catalogs: 2,800 semantic keys, equal key sets and preserved format arguments.
- Picker filter labels are translated; file-extension patterns are checked independently.
- Offline help: 44 matching topic IDs in Czech/English, valid internal/related links and illustration resources.
- All 22 help illustrations refreshed using generated demonstration data, with no user documents or accounts.
- Documentation local links and retained upstream license/source files checked by `python tools/check-release-content.py`.
- All 49 pinned runtime dependency hashes match the staged and installed files. Original upstream license text was retained.

## Selected application checks

| Check | Result |
|---|---|
| Language catalog/runtime | 2,816 assertions |
| Appearance and live language changes | 83 and 76 assertions |
| Native help | 179 assertions |
| Toolbar/dialog layouts | 59 assertions |
| Unified appearance | 15 assertions |
| Media conversion | 113 assertions |
| Media progress/ETA | 81 assertions |
| OpenMP integration | 12 assertions |
| Media responsive layout | 12 size/language combinations |
| New text documents | 28 assertions |
| SSH and ext reader | 17 and 68 assertions |
| Copy | Passed |
| Archives and locked-input interaction | 72 and 36 assertions |
| Analyzer and explicit Start/SMART types | 30 and 18 assertions |
| SMART | 54 assertions |
| Network progress and operation dialogs | 29 and 78 assertions |
| Vendor updates | 35 assertions |
| Dependency integrity and smoke tests | 41 and 22 assertions |

Archive temporary-directory cleanup and recursive-junction fixtures required execution outside the restricted test sandbox. Obsolete toolbar expectations for expanding options were changed to assert sidebar navigation, blue total/file progress and screen-bounded dialogs. The illustration harness now targets analyzer tabs instead of the removed diagnostics expander.

## Boundaries

The compiler still emits the existing PdfPig/System.Memory binding warning; Release also reports unused test-hook fields. The selected runtime checks passed. Physical USB formatting/ejection and all remote account/provider combinations were not exercised. Some MP3 duration estimates can still produce false output-validation rejection. See [distribution audit](DISTRIBUTION-AUDIT.md) for the remaining FFmpeg/Microsoft runtime public-distribution requirements. Account credentials and user profiles are excluded from the repository.

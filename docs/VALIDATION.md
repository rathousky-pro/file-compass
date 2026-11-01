# Validation — 1 November 2026

File Compass remains **0.9.5 beta**. The current maintenance development build is `20261101.66`; clean Release build `20261101.67` also compiled successfully. This is repository/installation maintenance, not a new public binary release or a claim that every historical test was rerun.

## Current checks

| Check | Result |
|---|---|
| Translation catalogs | Eight languages, 2,859 semantic keys, matching keys/placeholders |
| HTML help | 44 topic IDs in Czech and English; generated HTML, internal links, references and image hashes verified |
| Current screenshots | 28 matching scenes per language, 56 privacy-reviewed synthetic-demo images |
| Native help and About | 184 assertions; external edits load without recompiling; no help content embedded in EXE |
| Offline browser help | 25 checks across Czech/English, topic navigation, search, image zoom, history, language changes and mobile width |
| Language runtime | 2,878 assertions |
| Live language changes | 76 assertions with a fresh isolated test profile |
| Integrity protections | 41 checks |
| Application smoke test | 22 checks |
| Dependency notices | 49 runtime DLL/EXE/SFX files classified; 70 original legal/provenance files hashed |
| Installation and Release package | Runtime files, original notices and matching SMART sources agree with the reviewed inventory |
| Preview component manifest | All entries match source assets, including the managed WebView2 assemblies kept in ui |

The earlier 28 October feature regression record remains historical. Current changes concern documentation, external help, package metadata and bounded cleanup; they do not replace all prior copy, archive, network, cloud or hardware testing. Reusing an intentionally corrupted trusted-tools test fixture caused one live-language rerun to fail; a fresh profile passed all 76 checks. Application integrity enforcement was retained.

## Packaging and maintenance

Both build entry points now describe external HTML help instead of embedded JSON/images. The SDK project no longer copies publisher OAuth credentials or references removed documentation paths. PowerShell remains the locally exercised build path; an SDK build was not run because no .NET SDK is installed on this machine.

The current source uses FileCompass-named configuration/manifest inputs. The unused old File Voyager launcher source was removed; original source assets, runtime migration logic and dependency license texts were preserved. Build response files remain in use for paths with spaces and command-length limits.

Cleanup removed 364 obsolete outputs/backups containing 22,553 files (25,029,461,162 bytes, 23.31 GiB), with zero removal failures. The nested-repository guard was exercised and preserved its fixture. Cleanup previews/applied manifests and compact historical test results are retained locally under ignored `ai-docs/maintenance-20261101`. Cleanup checks Git tracking/ignore state, resolved workspace boundaries, running binaries, linked parents and nested repositories before deleting. The old blanket artifacts cleanup entry point now delegates to the bounded cleaner.

## Limits

The existing PdfPig/System.Memory binding warning remains; Release also reports unused test hooks. No claims are made about untested real provider accounts or physical USB operations. Original third-party notices are preserved verbatim. Exact FFmpeg/dependency corresponding sources and applicable Microsoft OpenMP runtime redistribution terms remain public-distribution prerequisites: see [distribution review](DISTRIBUTION-AUDIT.md). No new binary release was published and local profiles/credentials remain excluded from Git.

Czech uses the canonical identifier cs throughout the application, catalog cs.lng, HTML help folder cs and screenshot names. Earlier saved Czech identifiers are accepted and normalized; new saved selections use cs. The locale rename passed 2,878 language assertions, 184 native-help checks, 76 live-language checks, 72 appearance/language checks, 22 smoke checks and 25 offline browser checks.

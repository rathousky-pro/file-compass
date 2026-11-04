# Code signing policy

## Current status

File Compass releases are currently unsigned. SignPath Foundation has not
approved this project, and this page does not imply approval or an existing
certificate. The first intended signed release is **0.9.8**. Existing unsigned
releases will keep their identities and download hashes.

## Project and team

- Project: [File Compass](https://github.com/rathousky-pro/file-compass).
- Author, committer and reviewer: Jan Rathouský
  ([JRKladno76](https://github.com/JRKladno76)).
- Release signing approver: Jan Rathouský
  ([JRKladno76](https://github.com/JRKladno76)).
- Original application code: [MIT License](../LICENSE). Third-party components
  retain their own licenses; see [third-party notices](THIRD-PARTY-NOTICES.md).

GitHub two-factor authentication is enabled for the maintainer. MFA will also be
required for the maintainer's SignPath account before production signing.
External contributions and changes to build/signing workflows must be reviewed
by the maintainer. Each production signing request requires manual approval.

## Intended signing process

If accepted into the SignPath Foundation program, the intended attribution is:

Free code signing provided by [SignPath.io](https://signpath.io/), certificate
by [SignPath Foundation](https://signpath.org/).

This attribution is conditional on acceptance; the service is not active yet.

Production signing will use verified builds from the public source repository,
with all jobs leading to signing running on GitHub-hosted runners. A ZIP built
locally and merely uploaded to GitHub is not the intended signing input.
GitHub Actions integration for production signing is not configured yet.

Only binaries built from original File Compass sources will be submitted under
this project's signing policy. Upstream binaries will retain their existing
signatures or remain unsigned; they will not be represented as our own work.
Our embedded updater must be signed before embedding it in the application.
The final application and other original binaries are signed before generating
their release manifest, ZIP and SHA-256 checksums. Published unsigned assets
will not be silently replaced with different signed bytes.

Signatures and timestamp information must be verified before publication.
Tokens are held in CI secrets, never in source code, download packages or the
website. A custom build must not be presented as an official signed release.

## Privacy

See the [File Compass privacy policy](https://filecompass.rathousky.pro/privacy.html).
It describes application behavior, optional network/cloud providers and updates.
The signing process submits build artifacts and build-origin information to
SignPath; users' files, profiles, passwords and account tokens are excluded.

## Open eligibility questions

Acceptance is not assumed. Before production signing, resolve corresponding
source distribution for covered dependencies and applicable runtime terms.
The bundled 7-Zip unRAR restriction and Microsoft runtime components need
explicit eligibility review against SignPath Foundation's conditions.
See [distribution review](DISTRIBUTION-AUDIT.md).

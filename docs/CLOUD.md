# Cloud storage: Google Drive, OneDrive and Dropbox

New Google sign-ins use File Compass's publisher desktop OAuth configuration, `config/plugins/cloud/google-oauth.json`. Each user signs in with their own account without entering Client ID/Secret. Without publisher configuration, new Google sign-in is unavailable with an explanation. Existing accounts retain their old authorization identity until explicitly signed in again.

## Google registration status

As of **23 October 2026**, the external OAuth app is **in production but not verified**, requesting `https://www.googleapis.com/auth/drive`. Search Console website ownership is verified; Google branding verification still awaits propagation (Google requests a 24-hour wait before retrying). Restricted-scope review follows verified/published branding. An unverified-app warning and the 100-user project cap remain. Production status alone is not Google approval.

[Website](https://rathousky-pro.github.io/file-compass-web/) · [Privacy Policy](https://rathousky-pro.github.io/file-compass-web/privacy.html). Full Drive access enables management of existing folders, rather than only app-created files. Every user grants access separately and can revoke it. OneDrive/Dropbox do not use the Google client and are unaffected by Google's shared-client retirement.

## Add an account

1. Open Connections → Manage connections → Cloud storage, or the Connections toolbar button and Cloud storage tab.
2. Choose Add account, the provider and a local display name.
3. Choose Sign in in browser; select your account, complete MFA and review permissions on the provider's page.
4. OneDrive may ask for a drive/library; work/school accounts may need administrator approval.
5. When authorization succeeds, choose Connect.

Multiple accounts per provider are supported. Provider passwords are entered only in the provider's browser page; authorization returns automatically. The app name may appear as rclone depending on provider/client; Google's File Compass branding is awaiting verification. Use Sign in again if authorization expires or is cancelled. Provider questions include explanatory wording and access to the original question.

## Imported profiles

For accounts copied from another computer/Windows user, choose Sign in again. Authorization is independent of the original DPAPI key. A successful verified sign-in keeps the account name/identity and saves new local credentials; cancellation leaves the original profile unchanged. Other accounts/keys are unaffected.

The previous configuration remains in a protected `.before-signin-…` account backup. Remove account also removes these backups. Complete browser sign-in/MFA yourself.

## Files

Cloud panels support browsing, Copy/F5, Move/F6, folders, rename and delete through the shared network queue; browsing can continue during transfers. Tabs/workspaces remember account identity without forcing startup sign-in.

Interrupted cloud uploads restart the current file; completed targets can be skipped. Editing/previews use temporary downloads and normal limits. Native Google documents can be exported on download where supported. Operations depend on service/account capabilities.

Cloud panels do not provide SSH terminals or direct metadata search. This version does not mount cloud storage as Explorer drive letters and requires neither WinFsp nor administrator installation for cloud access.

## Disconnect, remove and revoke

Disconnect through Connections → Active connections or the connection-button context menu; saved authorization remains. Remove account deletes local configuration/tokens, not cloud files. Disconnect open panels and finish/remove pending transfers first. Revoke consent separately in Google/Microsoft/Dropbox account settings when required.

## Local storage and protection

Accounts are in `config/cloud`; each has an encrypted `rclone.conf` and a key protected with Windows DPAPI for the current user. Copying the program does not transfer usable authorization to another user/computer. Cloud directories have restricted permissions. Tokens are not process arguments or user log output.

A hidden local helper provides SFTP on `127.0.0.1` only, with per-account passwords and pinned host keys. It is not accessible from the LAN and stops with File Compass.

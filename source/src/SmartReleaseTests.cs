#if APP_TESTS
using System;using System.IO;using System.Diagnostics;using System.Text.RegularExpressions;using System.Threading;
namespace FileCompass {
 internal static class SmartReleaseTests {
    internal static int Run(string root){Directory.CreateDirectory(root);try{L.ChangeLanguage("cs-CZ");var release=VendorUpdates.Check("smart",CancellationToken.None);string path=UpdateRegistry.Resolve(VendorUpdates.Files("smart")[0]);string installed=FileVersionInfo.GetVersionInfo(path).ProductVersion??"";var match=Regex.Match(installed,@"\d+(\.\d+){1,3}");if(!match.Success||!VendorUpdates.ValidOrigin(release))throw new Exception("Missing installed version or official release origin");string status=UpdatesView.ReleaseStatus(release,match.Value);File.WriteAllLines(Path.Combine(root,"result.txt"),new[]{"Installed: "+match.Value,"Official release: "+release.Version,"Publisher SHA-256: "+(release.Hash??"unavailable"),"Status: "+status,"PASS: live official metadata and installed version compared"});return 0;}catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),"FAIL: "+ex);return 1;}}
 }
}
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
namespace CommanderCZ {
    // Publisher configuration, shared by all users. User tokens remain per account.
    internal sealed class GoogleDesktopOAuthDocument { public GoogleDesktopOAuthClient installed { get; set; } }
    internal sealed class GoogleDesktopOAuthClient { public string client_id { get; set; } public string client_secret { get; set; } }
    internal static class CloudApplicationOAuth {
        internal const string FileCompass = "FileCompass";
        internal static string TestPath;
        internal static string ConfigPath { get { return TestPath ?? Path.Combine(AppPaths.Resource("plugins"), "cloud", "google-oauth.json"); } }
        internal static GoogleDesktopOAuthClient LoadGoogle() {
            string file = ConfigPath;
            if (!File.Exists(file)) throw new IOException(L.Text("cloud.applicationNotConfigured"));
            try {
                if (new FileInfo(file).Length > 65536) throw new FormatException();
                var document = new JavaScriptSerializer { MaxJsonLength = 65536 }.Deserialize<GoogleDesktopOAuthDocument>(File.ReadAllText(file, new UTF8Encoding(false, true)));
                var client = document == null ? null : document.installed;
                if (client == null || String.IsNullOrWhiteSpace(client.client_id) || !Regex.IsMatch(client.client_id, @"\A[A-Za-z0-9._-]{1,220}\.apps\.googleusercontent\.com\z") || String.IsNullOrWhiteSpace(client.client_secret) || client.client_secret.Length > 512 || client.client_secret.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0) throw new FormatException();
                return client;
            } catch { throw new IOException(L.Text("cloud.applicationConfigInvalid")); }
        }
        internal static string AvailabilityError(string provider) {
            if (provider != "drive") return null;
            try { LoadGoogle(); return null; } catch (IOException ex) { return ex.Message; }
        }
        internal static void RequireReady(string provider) { if (provider == "drive") LoadGoogle(); }
        internal static void Apply(CloudAccount account, System.Diagnostics.ProcessStartInfo process) {
            if (account.Provider != "drive" || account.AuthorizationClient != FileCompass) return;
            var client = LoadGoogle();
            process.EnvironmentVariables["RCLONE_CONFIG_REMOTE_CLIENT_ID"] = client.client_id;
            process.EnvironmentVariables["RCLONE_CONFIG_REMOTE_CLIENT_SECRET"] = client.client_secret;
        }
        internal static string PublisherAnswer(CloudAccount account, CloudOption option) {
            if (account.Provider != "drive" || account.AuthorizationClient != FileCompass) return null;
            // A configured application must never fall back to rclone's retired identity
            // or ask the end user for publisher credentials.
            if (option.Name == "config_shared_client_id" || option.Name == "client_id" || option.Name == "client_secret") throw new IOException(L.Text("cloud.applicationIdentityRejected"));
            return null;
        }
    }
}

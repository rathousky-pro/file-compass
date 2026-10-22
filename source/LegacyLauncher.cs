// Compatibility entry point for existing CommanderCZ desktop shortcuts.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;

[assembly: AssemblyTitle("File Voyager launcher")]
[assembly: AssemblyProduct("File Voyager")]
internal static class LegacyLauncher
{
    static string Quote(string value)
    {
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            result.Append(c); slashes = 0;
        }
        result.Append('\\', slashes * 2); return result.Append('"').ToString();
    }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            string executable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileVoyager.exe");
            using (var process = Process.Start(new ProcessStartInfo(executable, String.Join(" ", args.Select(Quote))) { UseShellExecute = false })) { }
            return 0;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "File Voyager"); return 1; }
    }
}

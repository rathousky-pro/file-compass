using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CommanderCZ
{
    internal static class ApplicationShortcut
    {
        internal static string IconLocation(ApplicationIcon icon)
        {
            return Path.Combine(AppPaths.Resource("icons"), "FileCompass.Icons.dll") + "," + (int)ApplicationBrand.Valid(icon);
        }
        internal static void Create(string path, ApplicationIcon icon)
        {
            path = Path.GetFullPath(path);
            if (!Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase)) throw new IOException(L.Text("icons.shortcutExtension"));
            string executable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "File Compass.exe");
            string library = Path.Combine(AppPaths.Resource("icons"), "FileCompass.Icons.dll");
            if (!File.Exists(executable) || !File.Exists(library)) throw new IOException(L.Text("icons.invalidLibrary"));
            string temporary = Path.Combine(Path.GetDirectoryName(path), ".voyager-" + Guid.NewGuid().ToString("N") + ".lnk");
            object shell = null, shortcut = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
                shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { temporary });
                Set(shortcut, "TargetPath", executable);
                Set(shortcut, "WorkingDirectory", AppDomain.CurrentDomain.BaseDirectory);
                Set(shortcut, "Description", ApplicationBrand.Name);
                Set(shortcut, "IconLocation", IconLocation(icon));
                shortcut.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally
            {
                if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
                if (shell != null) Marshal.FinalReleaseComObject(shell);
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        static void Set(object shortcut, string property, string value)
        {
            shortcut.GetType().InvokeMember(property, BindingFlags.SetProperty, null, shortcut, new object[] { value });
        }
    }
}


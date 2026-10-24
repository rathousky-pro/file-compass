using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FileCompass
{
    public static class ToolbarThemeTests
    {
        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern uint ExtractIconEx(string file, int index, IntPtr[] large, IntPtr[] small, uint count);
        public static int Run(string workspace)
        {
            Directory.CreateDirectory(workspace);
            string root = Directory.CreateDirectory(Path.Combine(Path.GetFullPath(workspace), "theme-tests-" + Guid.NewGuid().ToString("N"))).FullName;
            var passed = new List<string>();
            try
            {
                string config = Path.Combine(root, "appearance.xml");
                foreach (ToolbarTheme theme in Enum.GetValues(typeof(ToolbarTheme)))
                {
                    new ToolbarThemeSettings { Theme = theme }.Save(config);
                    if (ToolbarThemeSettings.Load(config).Theme != theme) throw new Exception("Theme persistence failed.");
                    passed.Add(theme + " persisted and reloaded");
                }
                File.WriteAllText(config, "<invalid");
                if (ToolbarThemeSettings.Load(config).Theme != ToolbarTheme.Classic) throw new Exception("Malformed appearance must fall back to classic.");
                passed.Add("malformed config falls back safely");
                string library = Path.Combine(AppPaths.Resource("icons"), "FileCompass.Icons.dll");
                if (ExtractIconEx(library, -1, null, null, 0) != 7) throw new Exception("Windows must see seven icon groups in one library.");
                passed.Add("Windows enumerates all seven icons in one DLL");
                FluentTheme.Ensure();
                var originalIcon = ApplicationBrand.Current;
                var iconWindow = new Window { Icon = ApplicationBrand.Icon };
                var variants = new HashSet<string>();
                try
                {
                    foreach (ApplicationIcon variant in Enum.GetValues(typeof(ApplicationIcon)))
                    {
                        new ToolbarThemeSettings { Theme = ToolbarTheme.Outline, ApplicationIcon = variant }.Save(config);
                        var saved = ToolbarThemeSettings.Load(config);
                        if (saved.ApplicationIcon != variant || saved.Theme != ToolbarTheme.Outline) throw new Exception("Icon choice did not persist alongside theme.");
                        string link = Path.Combine(root, variant + ".lnk"); ApplicationShortcut.Create(link, variant);
                        object shell = null, shortcut = null;
                        try
                        {
                            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
                            shortcut = shell.GetType().InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { link });
                            string target = (string)shortcut.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null);
                            string location = (string)shortcut.GetType().InvokeMember("IconLocation", System.Reflection.BindingFlags.GetProperty, null, shortcut, null);
                            if (!target.Equals(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "File Compass.exe"), StringComparison.OrdinalIgnoreCase) || !location.Replace(", ", ",").Equals(ApplicationShortcut.IconLocation(variant), StringComparison.OrdinalIgnoreCase)) throw new Exception("Shortcut did not retain the executable and selected icon resource.");
                        }
                        finally { if (shortcut != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut); if (shell != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
                        var bitmap = (BitmapSource)ApplicationBrand.For(variant);
                        var pixels = new byte[bitmap.PixelHeight * bitmap.PixelWidth * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
                        variants.Add(Convert.ToBase64String(pixels));
                        ApplicationBrand.Apply(saved.ApplicationIcon);
                        if (iconWindow.Icon != bitmap || ApplicationBrand.Icon != bitmap || !bitmap.IsFrozen) throw new Exception("Live application icon did not update.");
                        var child = new Window(); CopyUi.Style(child, null, "Icon test", 300);
                        if (child.Icon != bitmap) throw new Exception("New windows must inherit chosen icon."); child.Close();
                    }
                    if (variants.Count != 7) throw new Exception("Application icons are not distinct.");
                    passed.Add("all seven icons are distinct, persist independently of theme and update existing and new windows");
                    passed.Add("Windows shortcuts retain all seven selected library icon indexes and the application target after reopening");
                }
                finally { ApplicationBrand.Apply(originalIcon); iconWindow.Close(); }
                foreach (string name in ToolbarThemeDialog.PreviewNames)
                {
                    var versions = new List<string>();
                    foreach (ToolbarTheme theme in Enum.GetValues(typeof(ToolbarTheme)))
                    {
                        var image = new Image { Width = 32, Height = 32, Source = DesktopIcons.ToolbarDrawing(name, theme) };
                        image.Measure(new Size(32, 32)); image.Arrange(new Rect(0, 0, 32, 32));
                        var bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32); bitmap.Render(image);
                        byte[] pixels = new byte[32 * 32 * 4]; bitmap.CopyPixels(pixels, 32 * 4, 0);
                        if (!pixels.Where((x, i) => i % 4 == 3).Any(x => x > 0)) throw new Exception("Blank icon: " + name);
                        versions.Add(Convert.ToBase64String(pixels));
                    }
                    if (versions.Distinct().Count() != 3) throw new Exception("Icon styles do not differ: " + name);
                }
                passed.Add("all " + ToolbarThemeDialog.PreviewNames.Length + " toolbar symbols render in three distinct styles");
                var dialog = new ToolbarThemeDialog(null); NetworkTests.Render(dialog, Path.Combine(workspace, "toolbar-themes.png")); dialog.Close();
                var main = new MainWindow(root); main.VerifyToolbarThemeChanges(workspace); main.Close();
                passed.Add("live toolbar updates without reopening the application");
                File.WriteAllLines(Path.Combine(workspace, "theme-test-result.txt"), passed.Concat(new[] { "PASS: " + passed.Count + " checks" })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(workspace, "theme-test-result.txt"), "FAIL: " + ex); return 1; }
            finally
            {
                if (!Path.GetFullPath(root).StartsWith(Path.GetFullPath(workspace).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("Unsafe cleanup.");
                Directory.Delete(root, true);
            }
        }
    }
    public partial class MainWindow
    {
        internal void VerifyToolbarThemeChanges(string workspace)
        {
            ApplyToolbarTheme(ToolbarTheme.Outline);
            foreach (ToolbarTheme theme in Enum.GetValues(typeof(ToolbarTheme)))
            {
                var before = toolbarImages.Select(x => x.Source).ToArray();
                ApplyToolbarTheme(theme);
                if (toolbarImages.Count == 0 || toolbarImages.Where((x, i) => x.Source == null || (ToolbarThemeDialog.PreviewNames.Contains((string)x.Tag) && x.Source == before[i])).Any()) throw new Exception("Toolbar image did not refresh.");
                NetworkTests.Render(this, Path.Combine(workspace, "toolbar-" + theme.ToString().ToLowerInvariant() + ".png"));
                var buttons = toolbarImages.Where(x => x.Parent is Button || (x.Parent is StackPanel && ((StackPanel)x.Parent).Parent is Button)).ToArray();
                foreach (var image in buttons)
                {
                    var button = image.Parent as Button ?? (Button)((StackPanel)image.Parent).Parent;
                    Rect bounds = image.TransformToAncestor(button).TransformBounds(new Rect(0, 0, image.ActualWidth, image.ActualHeight));
                    if (bounds.Left < 4 || bounds.Right > button.ActualWidth - 4 || bounds.Top < 4 || bounds.Bottom > button.ActualHeight - 4)
                        throw new Exception("Toolbar icon overlaps its frame: " + image.Tag);
                }
                var firstButton = buttons[0].Parent as Button ?? (Button)((StackPanel)buttons[0].Parent).Parent;
                var toolbar = (Border)((FrameworkElement)firstButton.Parent).Parent;
                var preview = new RenderTargetBitmap((int)Math.Ceiling(toolbar.ActualWidth), (int)Math.Ceiling(toolbar.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                var visual = new DrawingVisual(); using (var context = visual.RenderOpen()) context.DrawRectangle(new VisualBrush(toolbar), null, new Rect(0, 0, toolbar.ActualWidth, toolbar.ActualHeight));
                preview.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(preview));
                using (var stream = File.Create(Path.Combine(workspace, "toolbar-strip-" + theme.ToString().ToLowerInvariant() + ".png"))) encoder.Save(stream);
            }
        }
    }
}


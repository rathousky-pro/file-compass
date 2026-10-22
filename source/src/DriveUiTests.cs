using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CommanderCZ
{
    public static class DriveUiTests
    {
        public static int Run(string workspace)
        {
            Directory.CreateDirectory(workspace); var passed = new List<string>();
            try
            {
                var free = WindowsNetwork.FreeDriveLetters(new[] { "x:\\", "Y:", "Z:/", "D:" });
                if (free.First() != "W:" || free.Contains("X:") || free.Contains("D:") || free.Last() != "E:") throw new Exception("Highest free drive must be W; occupied letters must be excluded irrespective of case/root format.");
                if (WindowsNetwork.FreeDriveLetters(Enumerable.Range('D', 23).Select(x => ((char)x) + ":")).Length != 0) throw new Exception("No drive letter must be offered when all are occupied.");
                passed.Add("highest free letter, normalized occupied roots and exhausted letters");
                var roots = new List<string>(new[] { "C:\\", "X:\\", "Y:\\", "Z:\\" });
                foreach (string root in new[] { "W:\\", "E:\\", "V:\\" }) roots.Insert(FilePanel.DriveInsertionIndex(roots, root), root);
                if (!roots.SequenceEqual(new[] { "C:\\", "E:\\", "V:\\", "W:\\", "X:\\", "Y:\\", "Z:\\" })) throw new Exception("Newly mounted drives must be inserted in ascending letter order.");
                passed.Add("hot-added drive letters inserted before existing higher letters");
                foreach (int id in new[] { 7, 8, 9, 10, 11 })
                {
                    var icon = DesktopIcons.StockIcon(id);
                    if (!(icon is BitmapSource) || !icon.IsFrozen) throw new Exception("System drive icon must be a frozen Windows bitmap: " + id);
                    passed.Add("Windows stock drive icon " + id);
                }
                string system = Path.GetPathRoot(Environment.SystemDirectory);
                var actual = DesktopIcons.WindowsDriveIcon(system, false, true, true);
                if (!(actual is BitmapSource) || !actual.IsFrozen) throw new Exception("Actual system drive icon not obtained.");
                passed.Add("actual Windows system-drive icon retrieved");
                string tooltip = FilePanel.DriveTooltip("X:\\", "Backup", "\\\\10.0.10.101\\BACKUP", true, false);
                if (!tooltip.Contains("Backup") || !tooltip.Contains("\\\\10.0.10.101\\BACKUP") || !tooltip.Contains("Odpojeno")) throw new Exception("Network drive tooltip missing name, UNC or state.");
                passed.Add("drive tooltip includes volume, network address and disconnected state");
                int clicks = 0;
                var button = CompactTheme.Button(FilePanel.DriveLabel(system, false, true, actual), "Windows", delegate { clicks++; });
                button.Background = CompactTheme.ColorBrush("#D0E7FA");
                button.ApplyTemplate(); button.Measure(new Size(80, 24)); button.Arrange(new Rect(0, 0, 80, 24));
                var frame = (Border)button.Template.FindName("ContentBorder", button);
                var key = (DependencyPropertyKey)typeof(UIElement).GetField("IsMouseOverPropertyKey", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                button.SetValue(key, true);
                if (((SolidColorBrush)frame.Background).Color != (Color)ColorConverter.ConvertFromString("#E9EDF4") || clicks != 0)
                    throw new Exception("Hover must override local selection colours without navigating.");
                passed.Add("hover highlight appears before clicking despite explicit selected colours");
                button.SetValue(key, false);
                if (((SolidColorBrush)frame.Background).Color != (Color)ColorConverter.ConvertFromString("#D0E7FA")) throw new Exception("Selected disk colour not restored after hover.");
                passed.Add("leaving disk restores its selected colour");
                var row = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.White };
                foreach (string root in new[] { "C:\\", "D:\\", "E:\\" }) row.Children.Add(CompactTheme.Button(FilePanel.DriveLabel(root, false, true), root, delegate { }));
                foreach (string root in new[] { "Q:\\", "X:\\", "Y:\\", "Z:\\" }) row.Children.Add(CompactTheme.Button(FilePanel.DriveLabel(root, true, root != "Q:\\"), root, delegate { }));
                var hovered = CompactTheme.Button(FilePanel.DriveLabel("G:\\", false, true), "Games", delegate { }); row.Children.Add(hovered);
                var preview = new Window { Width = 450, Content = row, Background = Brushes.White };
                hovered.ApplyTemplate(); hovered.SetValue(key, true);
                NetworkTests.Render(preview, Path.Combine(workspace, "windows-drive-icons.png")); preview.Close();
                var newProfile = new NetworkProfileDialog(null, new NetworkProfile { Protocol = NetworkProtocol.SSHFS }, true);
                var selector = (ComboBox)typeof(NetworkProfileDialog).GetField("drive", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(newProfile);
                if ((string)selector.SelectedItem != WindowsNetwork.FreeDriveLetters().FirstOrDefault()) throw new Exception("New SSHFS profile must select highest free letter.");
                newProfile.Close();
                var existingProfile = new NetworkProfileDialog(null, new NetworkProfile { Protocol = NetworkProtocol.SSHFS, DriveLetter = "D:" });
                selector = (ComboBox)typeof(NetworkProfileDialog).GetField("drive", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(existingProfile);
                if ((string)selector.SelectedItem != "D:") throw new Exception("Editing an existing profile must preserve its drive letter.");
                existingProfile.Close(); passed.Add("new SSHFS defaults to free letter; existing profile retains configured letter");
                File.WriteAllLines(Path.Combine(workspace, "drive-test-result.txt"), passed); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(workspace, "drive-test-result.txt"), "FAIL: " + ex); return 1; }
        }
    }
}


using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FileCompass
{
    internal static class IconLibrary
    {
        static readonly object gate = new object();
        static readonly Dictionary<string, ImageSource> images = new Dictionary<string, ImageSource>();
        internal static readonly string[] Files = { "Actions.Fluent.fvicons", "Actions.Flat.fvicons", "Actions.Outline.fvicons" };
        internal static ImageSource Action(string name, ToolbarTheme theme)
        {
            int index = (int)theme; if (index < 0 || index >= Files.Length) index = 0;
            string key = index + ":" + name;
            lock (gate)
            {
                ImageSource image; if (images.TryGetValue(key, out image)) return image;
                string path = Path.Combine(AppPaths.Resource("icons"), Files[index]);
                // Data-only ZIP containers: no assembly loading or object deserialization.
                using (var input = File.OpenRead(path))
                using (var pack = new ZipArchive(input, ZipArchiveMode.Read))
                {
                    var entry = pack.GetEntry(name + ".png") ?? pack.GetEntry("document.png");
                    if (entry == null || entry.Length > 2 * 1024 * 1024) throw new InvalidDataException(L.Text("icons.invalidLibrary"));
                    using (var stream = entry.Open())
                    using (var bytes = new MemoryStream())
                    {
                        stream.CopyTo(bytes); bytes.Position = 0;
                        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = bytes; bitmap.EndInit(); bitmap.Freeze(); image = bitmap;
                    }
                }
                images[key] = image; return image;
            }
        }
    }
}


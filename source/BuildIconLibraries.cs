// Build-time asset exporter. Not shipped or executed by the application.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommanderCZ;

internal static class BuildIconLibraries
{
    static void Resource(BinaryWriter writer, ushort type, ushort id, byte[] bytes)
    {
        writer.Write(bytes.Length); writer.Write(32);
        writer.Write((ushort)65535); writer.Write(type); writer.Write((ushort)65535); writer.Write(id);
        writer.Write(0); writer.Write((ushort)0x1030); writer.Write((ushort)0x409); writer.Write(0); writer.Write(0);
        writer.Write(bytes); while (writer.BaseStream.Position % 4 != 0) writer.Write((byte)0);
    }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            string source = args[0], target = args[1], appIcons = args[2];
            typeof(FluentIcons).GetField("BuildSourceDirectory", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, source);
            string[] names = Directory.GetFiles(source, "*.png").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x).ToArray();
            string[] packs = { "Actions.Fluent.fvicons", "Actions.Flat.fvicons", "Actions.Outline.fvicons" };
            Directory.CreateDirectory(target);
            for (int theme = 0; theme < packs.Length; theme++)
                using (var output = File.Create(Path.Combine(target, packs[theme])))
                using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
                    foreach (string name in names)
                    {
                        ImageSource image = DesktopIcons.BuildToolbarDrawing(name, (ToolbarTheme)theme);
                        var visual = new DrawingVisual(); using (var draw = visual.RenderOpen()) draw.DrawImage(image, new Rect(0, 0, 128, 128));
                        var bitmap = new RenderTargetBitmap(128, 128, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
                        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                        using (var bytes = new MemoryStream())
                        {
                            png.Save(bytes); bytes.Position = 0;
                            using (var stream = zip.CreateEntry(name + ".png", CompressionLevel.Optimal).Open()) bytes.CopyTo(stream);
                        }
                    }
            using (var writer = new BinaryWriter(File.Create(Path.Combine(target, "ApplicationIcons.res"))))
            {
                Resource(writer, 0, 0, new byte[0]); ushort iconId = 1, groupId = 1;
                foreach (string name in new[] { "Compass", "Panels", "Route", "Night", "FloppyRoute", "FloppyOrbit", "FloppyClassic" })
                    using (var input = new BinaryReader(File.OpenRead(Path.Combine(appIcons, "FileVoyager-" + name + ".ico"))))
                    using (var data = new MemoryStream())
                    using (var group = new BinaryWriter(data))
                    {
                        input.ReadUInt16(); input.ReadUInt16(); ushort count = input.ReadUInt16();
                        group.Write((ushort)0); group.Write((ushort)1); group.Write(count);
                        for (int i = 0; i < count; i++)
                        {
                            byte[] metadata = input.ReadBytes(12); uint offset = input.ReadUInt32(); long next = input.BaseStream.Position;
                            group.Write(metadata); group.Write(iconId);
                            input.BaseStream.Position = offset;
                            Resource(writer, 3, iconId++, input.ReadBytes(BitConverter.ToInt32(metadata, 8)));
                            input.BaseStream.Position = next;
                        }
                        group.Flush(); Resource(writer, 14, groupId++, data.ToArray());
                    }
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

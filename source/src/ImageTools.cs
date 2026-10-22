using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CommanderCZ
{
    internal sealed class ImageToolOptions
    {
        internal int Rotation, MaxEdge = 1920, Percent = 50, ResizeMode = 1, Quality = 90;
        internal string Format = "PNG", OutputFolder;
    }
    internal static class ImageTools
    {
        internal static string Extension(string format) { return format == "JPEG" ? ".jpg" : format == "BMP" ? ".bmp" : format == "TIFF" ? ".tiff" : ".png"; }
        internal static string Output(string source, ImageToolOptions options) { return Path.Combine(options.OutputFolder,Path.GetFileNameWithoutExtension(source) + "_upraveno" + Extension(options.Format)); }
        internal static string[] Plan(string[] sources, ImageToolOptions options)
        {
            if (!Directory.Exists(options.OutputFolder) || !new[] { "PNG","JPEG","BMP","TIFF" }.Contains(options.Format) ||
                !new[] { 0,90,180,270 }.Contains(options.Rotation) || options.ResizeMode < 0 || options.ResizeMode > 2 ||
                options.MaxEdge < 1 || options.MaxEdge > 32768 || options.Percent < 1 || options.Percent > 100 || options.Quality < 1 || options.Quality > 100)
                throw new IOException(L.Text("imageTools.invalidOptions"));
            string[] outputs = sources.Select(x => Output(x,options)).ToArray();
            if (outputs.Distinct(StringComparer.OrdinalIgnoreCase).Count() != outputs.Length || outputs.Any(x => File.Exists(x) || Directory.Exists(x))) throw new IOException(L.Text("parts.outputExists"));
            if (sources.Any(x => !File.Exists(x) || (File.GetAttributes(x) & FileAttributes.ReparsePoint) != 0)) throw new IOException(L.Text("parts.linkRejected"));
            return outputs;
        }
        internal static void Process(string[] sources, ImageToolOptions options, CancellationToken token, Action<string> progress)
        {
            string[] outputs = Plan(sources,options);
            for (int i = 0; i < sources.Length; i++) { token.ThrowIfCancellationRequested(); Transform(sources[i],outputs[i],options,token); if (progress != null) progress(Path.GetFileName(sources[i]) + " · " + ((i+1)*100/sources.Length) + "%"); }
        }
        internal static void OnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(delegate() { try { action(); } catch (Exception ex) { failure = ex; } }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
        static BitmapSource Orient(BitmapSource bitmap, BitmapMetadata metadata)
        {
            int orientation = 1;
            try { if (metadata != null && metadata.ContainsQuery("/app1/ifd/{ushort=274}")) orientation = Convert.ToInt32(metadata.GetQuery("/app1/ifd/{ushort=274}")); else if (metadata != null && metadata.ContainsQuery("/ifd/{ushort=274}")) orientation = Convert.ToInt32(metadata.GetQuery("/ifd/{ushort=274}")); } catch (NotSupportedException) { }
            var group = new TransformGroup();
            if (new[] {2,5,7}.Contains(orientation)) group.Children.Add(new ScaleTransform(-1,1));
            if (orientation == 4) group.Children.Add(new ScaleTransform(1,-1));
            if (orientation == 3) group.Children.Add(new RotateTransform(180));
            else if (orientation == 7 || orientation == 6) group.Children.Add(new RotateTransform(90));
            else if (orientation == 5 || orientation == 8) group.Children.Add(new RotateTransform(270));
            return group.Children.Count == 0 ? bitmap : new TransformedBitmap(bitmap,group);
        }
        internal static void Transform(string source, string target, ImageToolOptions options, CancellationToken token)
        {
            string stage = Path.Combine(Path.GetDirectoryName(target),".CommanderCZ-image-" + Guid.NewGuid().ToString("N"));
            try {
                using (var input = new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                    var decoder = BitmapDecoder.Create(input,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count > 1 && options.Format != "TIFF") throw new IOException(L.Text("imageTools.multiframe"));
                    if (decoder.Frames.Sum(x => (long)x.PixelWidth*x.PixelHeight) > 200000000 || decoder.Frames.Count > 1024) throw new IOException(L.Text("imageTools.pixelLimit"));
                    BitmapEncoder encoder = options.Format == "JPEG" ? (BitmapEncoder)new JpegBitmapEncoder { QualityLevel = options.Quality } : options.Format == "BMP" ? new BmpBitmapEncoder() : options.Format == "TIFF" ? new TiffBitmapEncoder { Compression = TiffCompressOption.Zip } : (BitmapEncoder)new PngBitmapEncoder();
                    foreach (var frame in decoder.Frames) {
                        token.ThrowIfCancellationRequested();
                        BitmapSource bitmap = Orient(frame,frame.Metadata as BitmapMetadata);
                        if (options.Rotation != 0) bitmap = new TransformedBitmap(bitmap,new RotateTransform(options.Rotation));
                        double scale = options.ResizeMode == 1 ? Math.Min(1.0, (double)options.MaxEdge/Math.Max(bitmap.PixelWidth,bitmap.PixelHeight)) : options.ResizeMode == 2 ? options.Percent/100.0 : 1.0;
                        if (scale != 1.0) bitmap = new TransformedBitmap(bitmap,new ScaleTransform(scale,scale));
                        if (options.Format == "JPEG" || options.Format == "BMP") {
                            var visual = new DrawingVisual();
                            using (var draw = visual.RenderOpen()) { draw.DrawRectangle(Brushes.White,null,new Rect(0,0,bitmap.PixelWidth,bitmap.PixelHeight)); draw.DrawImage(bitmap,new Rect(0,0,bitmap.PixelWidth,bitmap.PixelHeight)); }
                            var opaque = new RenderTargetBitmap(bitmap.PixelWidth,bitmap.PixelHeight,96,96,PixelFormats.Pbgra32); opaque.Render(visual); bitmap = new FormatConvertedBitmap(opaque,PixelFormats.Bgr24,null,0);
                        }
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    }
                    using (var output = new FileStream(stage,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { encoder.Save(output); output.Flush(true); }
                }
                token.ThrowIfCancellationRequested();
                using (var verify = File.OpenRead(stage)) { var decoder = BitmapDecoder.Create(verify,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad); if (decoder.Frames.Count < 1 || decoder.Frames.Any(x => x.PixelWidth < 1 || x.PixelHeight < 1)) throw new IOException(L.Text("imageTools.verifyFailed")); }
                File.Move(stage,target);
            } finally { if (File.Exists(stage)) File.Delete(stage); }
        }
    }
}

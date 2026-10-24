using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace FileCompass {
    internal static class FileMetadataTests {
        static void Package(string path,params string[] parts) {using(var file=File.Create(path))using(var zip=new ZipArchive(file,ZipArchiveMode.Create))for(int i=0;i<parts.Length;i+=2)using(var writer=new StreamWriter(zip.CreateEntry(parts[i]).Open(),Encoding.UTF8))writer.Write(parts[i+1]);}
        static string Get(FileDetails data,string key){var row=data.Rows.FirstOrDefault(x=>x.Label==L.Text("details."+key));return row==null?null:row.Value;}
        internal static int Run(string root) {
            Directory.CreateDirectory(root);var passed=new List<string>();MainWindow main=null;
            Action<bool,string> check=(valid,label)=>{if(!valid)throw new Exception(label);passed.Add(label);File.AppendAllText(Path.Combine(root,"metadata-progress.txt"),label+Environment.NewLine);};
            try {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                var pixels=new byte[320*200*3];for(int i=0;i<pixels.Length;i++)pixels[i]=(byte)(i%255);
                var bitmap=BitmapSource.Create(320,200,96,96,PixelFormats.Rgb24,null,pixels,320*3);
                string png=Path.Combine(root,"Fotografie.png");var pngEncoder=new PngBitmapEncoder();pngEncoder.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(png))pngEncoder.Save(output);
                var image=FileMetadata.Read(png);check(Get(image,"dimensions")=="320 × 200 px","Image dimensions use original pixels rather than scaled preview");
                string jpg=Path.Combine(root,"EXIF.jpg");var jpgEncoder=new JpegBitmapEncoder();jpgEncoder.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(jpg))jpgEncoder.Save(output);
                using(var tag=TagLib.File.Create(jpg)){tag.GetTag(TagLib.TagTypes.TiffIFD,true);var photo=(TagLib.Image.CombinedImageTag)tag.Tag;photo.Make="Compass Camera";photo.Model="Test Model";photo.DateTime=new DateTime(2024,6,15,12,30,0);tag.Save();}
                var exif=FileMetadata.Read(jpg);check(Get(exif,"camera")=="Test Model"&&Get(exif,"taken")!=null,"EXIF camera and capture date are extracted");
                string ico=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","..","..","source","assets","file-voyager","FileVoyager-FloppyOrbit.ico"));
                var icon=FileMetadata.Read(ico);check(Get(icon,"iconSizes").Contains("256 × 256"),"ICO lists embedded image sizes");
                string docx=Path.Combine(root,"Dokument.docx");
                Package(docx,"docProps/core.xml","<core><title>Test title</title><creator>Compass Author</creator></core>","docProps/app.xml","<Properties><Pages>3</Pages><Words>25</Words></Properties>","word/document.xml","<document><body><p><t>Hello document details.</t></p><p><t>Second paragraph.</t></p></body></document>");
                var word=FileMetadata.Read(docx);check(Get(word,"author")=="Compass Author"&&Get(word,"cachedPages")=="3"&&word.Excerpt.Contains("Hello document"),"Word exposes metadata, cached page count and text excerpt");
                string xlsx=Path.Combine(root,"Tabulka.xlsx");Package(xlsx,"xl/workbook.xml","<workbook xmlns:r='rel'><sheets><sheet name='Data' r:id='rId1'/><sheet name='Hidden' state='hidden' r:id='rId2'/></sheets></workbook>","xl/_rels/workbook.xml.rels","<Relationships><Relationship Id='rId1' Target='worksheets/sheet1.xml'/></Relationships>","xl/worksheets/sheet1.xml","<worksheet><dimension ref='A1:C12'/></worksheet>","xl/sharedStrings.xml","<sst><si><t>Example cell</t></si></sst>");
                var excel=FileMetadata.Read(xlsx);check(Get(excel,"sheets")=="2"&&Get(excel,"range").Contains("A1:C12")&&Get(excel,"sheetNames").Contains("hidden"),"Excel exposes sheets, hidden state and stored used ranges");
                string odt=Path.Combine(root,"Dokument.odt");Package(odt,"meta.xml","<root xmlns:meta='meta'><meta:initial-creator>ODF Author</meta:initial-creator><meta:document-statistic meta:page-count='2'/></root>","content.xml","<root><p>OpenDocument text.</p></root>");
                var odf=FileMetadata.Read(odt);check(Get(odf,"author")=="ODF Author"&&odf.Excerpt.Contains("OpenDocument"),"OpenDocument metadata and excerpt are available");
                string pdf=Path.Combine(root,"Dokument.pdf");var builder=new UglyToad.PdfPig.Writer.PdfDocumentBuilder();var font=builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);builder.DocumentInformation.Title="Compass PDF";
                builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4).AddText("Hello PDF details",12,new UglyToad.PdfPig.Core.PdfPoint(30,700),font);builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);File.WriteAllBytes(pdf,builder.Build());
                var pdfInfo=FileMetadata.Read(pdf);check(Get(pdfInfo,"pages")=="2"&&Get(pdfInfo,"title")=="Compass PDF"&&pdfInfo.Excerpt.Contains("Hello PDF"),"PDF parser reads actual pages, title and first-page text");
                string json=Path.Combine(root,"data.json");File.WriteAllText(json,"{\"one\":1,\"two\":2}",new UTF8Encoding(false));check(Get(FileMetadata.Read(json),"entries")=="2","JSON exposes root entry count");
                string csv=Path.Combine(root,"data.csv");File.WriteAllText(csv,"\"a;b\";c;d\n1;2;3");check(Get(FileMetadata.Read(csv),"columns")=="3","CSV column count respects quoted separators");
                string svg=Path.Combine(root,"image.svg");File.WriteAllText(svg,"<svg width='100' height='60' viewBox='0 0 100 60'/>");check(Get(FileMetadata.Read(svg),"viewBox")=="0 0 100 60","SVG exposes viewBox");
                var exe=FileMetadata.Read(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);check(Get(exe,"architecture")=="x64"&&Get(exe,"product")=="File Compass","Executable metadata is read without loading or executing it");
                string bad=Path.Combine(root,"bad.docx");Package(bad,"word/document.xml","<!DOCTYPE foo [<!ENTITY xxe SYSTEM 'file:///C:/Windows/win.ini'>]><document>&xxe;</document>");var malicious=FileMetadata.Read(bad);check(malicious.Excerpt==null&&Get(malicious,"notice")!=null,"Document metadata rejects external XML entities");
                using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();bool stopped=false;try{NavigationTests.Wait(FileMetadataService.Load(png,cancelled.Token));}catch(OperationCanceledException){stopped=true;}check(stopped,"Cancelled metadata requests do not launch a reader");}
                var isolated=FileMetadataService.Load(pdf,CancellationToken.None);NavigationTests.Wait(isolated);check(Get(isolated.Result,"pages")=="2","Isolated metadata worker returns PDF details through bounded JSON");
                string wav=Path.Combine(root,"Zvuk.wav");using(var output=File.Create(wav))using(var writer=new BinaryWriter(output)){int length=44100;writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+length);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(44100);writer.Write(44100);writer.Write((short)1);writer.Write((short)8);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(length);writer.Write(new byte[length]);}
                var audio=FileMetadata.Read(wav);check(Get(audio,"duration")!=null&&Get(audio,"sampleRate")=="44100","Audio exposes duration and sample rate");
                string archive=Path.Combine(root,"Archive.7z");ArchiveEngine.Create(archive,new[]{png},ArchiveSettings.Load(),CancellationToken.None);
                var archiveInfo=FileMetadata.Read(archive);check(Get(archiveInfo,"files")=="1"&&Get(archiveInfo,"expandedSize")!=null,"7z archive exposes file count and uncompressed size");
                string fontPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf");
                if(File.Exists(fontPath)){var fontInfo=FileMetadata.Read(fontPath);check(Get(fontInfo,"fontFamily")!=null&&Get(fontInfo,"glyphs")!=null,"Font exposes family and mapped character count");}
                var deadline=System.Diagnostics.Stopwatch.StartNew();using(var cancellation=new CancellationTokenSource()){var pending=FileMetadataService.Load(pdf,cancellation.Token);cancellation.Cancel();bool stopped=false;try{NavigationTests.Wait(pending);}catch(OperationCanceledException){stopped=true;}check(stopped&&deadline.ElapsedMilliseconds<3000,"Cancellation stops an in-flight metadata worker promptly");}
                main=new MainWindow(root,new ProgramSettings{AutoRefresh=false}){Width=1400,Height=900,ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};main.Show();NavigationTests.WaitPanels(main);
                NavigationTests.Wait(main.CheckMetadataPreview(png,pdf,root,check));
                File.WriteAllLines(Path.Combine(root,"metadata-result.txt"),passed.Concat(new[]{"PASS: "+passed.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"metadata-result.txt"),String.Join("\n",passed)+"\nFAIL: "+ex);return 1;}finally{if(main!=null)main.Close();}
        }
    }
    public partial class MainWindow {
        internal async Task CheckMetadataPreview(string png,string pdf,string root,Action<bool,string> check) {
            var window=new Window();CopyUi.Style(window,this,"Metadata preview",800);window.Height=750;window.SizeToContent=SizeToContent.Manual;window.Left=-18000;window.Top=-18000;window.WindowStartupLocation=WindowStartupLocation.Manual;
            var pane=new QuickPreviewPane(window.Close);window.Content=pane;window.Show();
            try {
                pane.ShowFile(png);await pane.LoadingTask;await pane.MetadataLoadingTask;check(pane.Metadata!=null&&pane.Metadata.Summary.Contains("320 × 200"),"Shared preview displays image details");
                AdvancedToolsTests.Visual<Expander>(pane).Single().IsExpanded=true;await Task.Delay(250);window.UpdateLayout();NextStageTests.Draw(window,Path.Combine(root,"image-details.png"));
                pane.ShowFile(pdf);await pane.LoadingTask;await pane.MetadataLoadingTask;check(AdvancedToolsTests.Visual<TextBox>(pane).Any(x=>x.Text.Contains("Hello PDF")),"PDF auto preview shows extracted text instead of binary data");
                window.UpdateLayout();NextStageTests.Draw(window,Path.Combine(root,"pdf-details.png"));
                pane.ShowFile(pdf);pane.ShowFile(png);await pane.LoadingTask;await pane.MetadataLoadingTask;check(pane.Metadata.Summary.Contains("320 × 200"),"Rapid selection changes cannot show details from an earlier file");
                pane.ShowMessage("No file");check(pane.Metadata==null,"Clearing preview also clears metadata");
                SetPanelLayout("Single");left.Navigate(root,false);await left.NavigationTask;left.List.SelectedItem=left.List.Items.Cast<Entry>().First(x=>x.FullPath==png);left.SetDetailExpanded(true);if(left.DetailLoadingTask!=null)await left.DetailLoadingTask;
                var compact=AdvancedToolsTests.Visual<QuickPreviewPane>(left).Single();if(compact.MetadataLoadingTask!=null)await compact.MetadataLoadingTask;check(compact.Metadata!=null,"Single-panel bottom preview uses the same metadata reader");
                AdvancedToolsTests.Visual<Expander>(compact).Single().IsExpanded=true;await Task.Delay(250);UpdateLayout();check(AdvancedToolsTests.Visual<System.Windows.Controls.Image>(compact).Any(x=>x.Source!=null&&x.ActualHeight>=35&&x.ActualWidth>=35),"Expanded compact details leave room for the actual image preview");NextStageTests.Draw(this,Path.Combine(root,"single-panel-details.png"));
            }finally{pane.DisposePreview();window.Close();}
        }
    }
}




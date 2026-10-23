using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;

namespace CommanderCZ
{
    internal sealed class RichFilePreview : Grid
    {
        readonly TextEditor code = new TextEditor { IsReadOnly = true, ShowLineNumbers = true, FontFamily = new FontFamily("Consolas"), FontSize = 13, Foreground = FluentTheme.Text, Background = CompactTheme.WhiteSurface, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        readonly FlowDocumentScrollViewer markdown = new FlowDocumentScrollViewer { IsToolBarVisible = false, IsSelectionEnabled = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = CompactTheme.WhiteSurface };
        static readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder().DisableHtml().UsePipeTables().Build();
        internal static bool IsMarkdown(string path) { string ext = Path.GetExtension(path).ToLowerInvariant(); return ext == ".md" || ext == ".markdown" || ext == ".mdown"; }
        internal static bool Supports(string path) { return IsMarkdown(path) || DocumentPreview.IsHtml(path) || HighlightingManager.Instance.GetDefinitionByExtension(Path.GetExtension(path)) != null || Path.GetExtension(path).Equals(".lng", StringComparison.OrdinalIgnoreCase); }
        internal RichFilePreview() { Children.Add(code); Children.Add(markdown); ICSharpCode.AvalonEdit.Search.SearchPanel.Install(code); }
        internal static MarkdownDocument Parse(string source) { return Markdown.Parse(source, pipeline); }
        internal void Show(string path, string source, MarkdownDocument parsed)
        {
            bool md = IsMarkdown(path); code.Visibility = md ? Visibility.Collapsed : Visibility.Visible; markdown.Visibility = md ? Visibility.Visible : Visibility.Collapsed;
            if (!md)
            {
                code.SyntaxHighlighting = DocumentPreview.IsHtml(path)?HighlightingManager.Instance.GetDefinition("HTML"):HighlightingManager.Instance.GetDefinitionByExtension(Path.GetExtension(path)) ?? (Path.GetExtension(path).Equals(".lng", StringComparison.OrdinalIgnoreCase) ? HighlightingManager.Instance.GetDefinition("JavaScript") : null);
                code.Text = source; code.ScrollToHome(); markdown.Document = null; return;
            }
            code.Text = "";
            var document = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 13, PagePadding = new Thickness(14), Background = CompactTheme.WhiteSurface, Foreground = FluentTheme.Text };
            int budget = 4000; Blocks(parsed ?? Parse(source), document.Blocks, 0, ref budget); markdown.Document = document;
        }
        internal void Clear() { code.Text = ""; markdown.Document = null; }
        static void InlineNodes(ContainerInline source, InlineCollection target, int depth, ref int budget)
        {
            if (source == null || depth > 20) return;
            foreach (var item in source)
            {
                if (--budget < 0) return;
                var literal = item as LiteralInline; var inlineCode = item as CodeInline; var emphasis = item as EmphasisInline; var link = item as LinkInline;
                if (literal != null) target.Add(new Run(literal.Content.ToString()));
                else if (inlineCode != null) target.Add(new Run(inlineCode.Content) { FontFamily = new FontFamily("Consolas"), Background = CompactTheme.PanelSurface });
                else if (emphasis != null) { var span = new Span(); if (emphasis.DelimiterCount >= 2) span.FontWeight = FontWeights.Bold; else span.FontStyle = FontStyles.Italic; InlineNodes(emphasis, span.Inlines, depth + 1, ref budget); target.Add(span); }
                else if (link != null) { var span = new Span { Foreground = FluentTheme.Accent, ToolTip = link.Url }; InlineNodes(link, span.Inlines, depth + 1, ref budget); target.Add(span); }
                else if (item is LineBreakInline) target.Add(new LineBreak());
                else if (item is AutolinkInline) target.Add(new Run(((AutolinkInline)item).Url) { Foreground = FluentTheme.Accent });
                else if (item is ContainerInline) InlineNodes((ContainerInline)item, target, depth + 1, ref budget);
            }
        }
        static void Blocks(ContainerBlock source, BlockCollection target, int depth, ref int budget)
        {
            if (depth > 20) return;
            foreach (var block in source)
            {
                if (--budget < 0) { target.Add(new Paragraph(new Run(L.Text("preview.structureLimit")))); return; }
                var heading = block as HeadingBlock; var paragraph = block as ParagraphBlock; var codeBlock = block as CodeBlock;
                var list = block as ListBlock; var quote = block as QuoteBlock; var table = block as Markdig.Extensions.Tables.Table;
                if (heading != null) { var p = new Paragraph { FontSize = 26 - Math.Min(heading.Level, 6) * 2, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 6) }; InlineNodes(heading.Inline, p.Inlines, depth, ref budget); target.Add(p); }
                else if (paragraph != null) { var p = new Paragraph { Margin = new Thickness(0, 3, 0, 8) }; InlineNodes(paragraph.Inline, p.Inlines, depth, ref budget); target.Add(p); }
                else if (codeBlock != null) target.Add(new Paragraph(new Run(codeBlock.Lines.ToString())) { FontFamily = new FontFamily("Consolas"), Background = CompactTheme.PanelSurface, Padding = new Thickness(8), Margin = new Thickness(0, 5, 0, 8) });
                else if (list != null)
                {
                    var output = new System.Windows.Documents.List { MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Margin = new Thickness(15, 2, 0, 6), Padding = new Thickness(12, 0, 0, 0) };
                    foreach (var item in list) { var child = item as ContainerBlock; if (child == null || --budget < 0) break; var li = new ListItem(); Blocks(child, li.Blocks, depth + 1, ref budget); output.ListItems.Add(li); } target.Add(output);
                }
                else if (quote != null) { var section = new Section { BorderBrush = CompactTheme.ChromeLine, BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(10, 0, 0, 0), Foreground = FluentTheme.Muted }; Blocks(quote, section.Blocks, depth + 1, ref budget); target.Add(section); }
                else if (table != null)
                {
                    var output = new System.Windows.Documents.Table { CellSpacing = 0 }; var group = new TableRowGroup(); output.RowGroups.Add(group);
                    foreach (Markdig.Extensions.Tables.TableRow row in table)
                    {
                        if (--budget < 0) break; var outputRow = new System.Windows.Documents.TableRow(); group.Rows.Add(outputRow); int count = 0;
                        foreach (Markdig.Extensions.Tables.TableCell cell in row) { if (++count > 24 || --budget < 0) break; var outputCell = new System.Windows.Documents.TableCell { BorderBrush = CompactTheme.Line, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(6), FontWeight = row.IsHeader ? FontWeights.Bold : FontWeights.Normal }; Blocks(cell, outputCell.Blocks, depth + 1, ref budget); outputRow.Cells.Add(outputCell); }
                    }
                    target.Add(output);
                }
                else if (block is ThematicBreakBlock) target.Add(new Paragraph { BorderBrush = CompactTheme.Line, BorderThickness = new Thickness(0, 1, 0, 0) });
                else if (block is ContainerBlock) Blocks((ContainerBlock)block, target, depth + 1, ref budget);
            }
        }
    }
}


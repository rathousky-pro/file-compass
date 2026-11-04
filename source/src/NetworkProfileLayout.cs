using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace FileCompass {
    public sealed partial class NetworkProfileDialog {
        internal Rect? LayoutWorkArea;
        bool profileFitQueued, fittingProfile;
        Rect lastProfileWorkArea;
        Grid generalCardsGrid;
        GroupBox[] generalCards;
        int generalColumns;
        static Grid ProfileRow(UIElement[] children, int columns) {
            var grid = new Grid(); for (int i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < children.Length; i++) {
                if (i % columns == 0) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var child = (FrameworkElement)children[i]; child.Margin = new Thickness(i % columns == 0 ? 0 : 6, 0, i % columns == columns - 1 ? 0 : 6, 8);
                Grid.SetRow(child, i / columns); Grid.SetColumn(child, i % columns); grid.Children.Add(child);
            }
            return grid;
        }
        void ArrangeProfilePages() {
            var groups = general.Children.OfType<GroupBox>().ToArray();
            generalCards = groups; generalColumns = 0;
            foreach (var card in groups) general.Children.Remove(card);
            generalCardsGrid = new Grid(); general.Children.Insert(0, generalCardsGrid); ArrangeGeneralCards(2);
            var group = (GroupBox)advanced.Children[0]; var details = (StackPanel)group.Content;
            var children = details.Children.Cast<UIElement>().ToArray(); details.Children.Clear();
            var fields = children.Where(x => x is Grid || x is CheckBox).ToArray();
            if (fields.Length > 0) details.Children.Add(ProfileRow(fields, 2));
            foreach (var child in children.Except(fields)) {
                var expander = child as Expander;
                if (expander != null) {
                    var content = (StackPanel)expander.Content; var rows = content.Children.Cast<UIElement>().ToArray(); content.Children.Clear(); expander.Content = null;
                    details.Children.Add(new GroupBox { Header = expander.Header, Content = ProfileRow(rows, 2), Margin = new Thickness(0, 6, 0, 6) });
                } else details.Children.Add(child);
            }
        }
        void ArrangeGeneralCards(int columns) {
            if (generalColumns == columns) return; generalColumns = columns;
            generalCardsGrid.Children.Clear(); generalCardsGrid.ColumnDefinitions.Clear(); generalCardsGrid.RowDefinitions.Clear();
            for (int i = 0; i < columns; i++) generalCardsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < generalCards.Length; i++) {
                if (i % columns == 0) generalCardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var card = generalCards[i]; bool fullRow = columns == 2 && i == 2;
                card.Margin = new Thickness(i % columns == 0 ? 0 : 6, 0, fullRow || i % columns == columns - 1 ? 0 : 6, 8);
                Grid.SetColumn(card, i % columns); Grid.SetRow(card, i / columns); Grid.SetColumnSpan(card, fullRow ? columns : 1); generalCardsGrid.Children.Add(card);
            }
        }
        void QueueProfileFit() {
            if (profileFitQueued || fittingProfile) return; profileFitQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate { profileFitQueued = false; FitProfilePage(); }));
        }
        internal void FitProfilePage() {
            if (!IsLoaded || fittingProfile) return;
            fittingProfile = true;
            try {
                var area = lastProfileWorkArea = LayoutWorkArea ?? DialogGeometry.WorkArea(this);
                MinWidth = Math.Min(820, area.Width - 32); MinHeight = Math.Min(350, area.Height - 32); MaxWidth = area.Width - 32; MaxHeight = area.Height - 32;
                ArrangeGeneralCards(area.Height < 680 ? 3 : 2);
                FluentDialogUx.Refresh(this);
                UpdateLayout(); var scroll = ((TabItem)Tabs.SelectedItem).Content as ScrollViewer;
                var content = (FrameworkElement)scroll.Content;
                content.Measure(new Size(Math.Max(100, scroll.ActualWidth - SystemParameters.VerticalScrollBarWidth), Double.PositiveInfinity));
                Height = Math.Min(MaxHeight, Math.Max(MinHeight, Math.Ceiling(content.DesiredSize.Height + ActualHeight - scroll.ActualHeight + 8)));
                if (new Rect(Left, Top, ActualWidth, ActualHeight).IntersectsWith(area)) { Left = Math.Max(area.Left + 16, Math.Min(Left, area.Right - Width - 16)); Top = Math.Max(area.Top + 16, Math.Min(Top, area.Bottom - Height - 16)); }
                UpdateLayout();
            } finally { fittingProfile = false; }
        }
    }
}

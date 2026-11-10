using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FileCompass
{
    public sealed class NetworkScheduleRule
    {
        public int Days = 127, Start = 480, End = 1320;
        public long KiB = 1024;
        internal NetworkScheduleRule Copy() { return (NetworkScheduleRule)MemberwiseClone(); }
    }
    internal static class NetworkSchedule
    {
        static readonly object Gate = new object(); static ProgramSettings current;
        internal static List<NetworkScheduleRule> Rules(ProgramSettings s)
        {
            if (s.NetworkScheduleRules != null) return s.NetworkScheduleRules.Where(x => x != null).Take(4).Select(x => x.Copy()).ToList();
            return new List<NetworkScheduleRule> { new NetworkScheduleRule { Days = s.NetworkScheduleDays, Start = s.NetworkScheduleStart, End = s.NetworkScheduleEnd, KiB = s.NetworkScheduleKiB } };
        }
        internal static void Update(ProgramSettings s) { lock (Gate) current = new ProgramSettings { NetworkScheduleEnabled = s.NetworkScheduleEnabled, NetworkScheduleRules = Rules(s) }; }
        internal static long Planned(DateTime now)
        {
            ProgramSettings s; lock (Gate) { if (current == null) { var loaded = ProgramSettings.Load(); current = new ProgramSettings { NetworkScheduleEnabled = loaded.NetworkScheduleEnabled, NetworkScheduleRules = Rules(loaded) }; } s = current; }
            return Scheduled(s, now);
        }
        internal static long Effective(long manual, DateTime now)
        {
            long planned = Planned(now); return manual == 0 ? planned : planned == 0 ? manual : Math.Min(manual, planned);
        }
        internal static long Scheduled(ProgramSettings s, DateTime now)
        {
            if (!s.NetworkScheduleEnabled) return 0;
            long result = 0;
            foreach (var rule in s.NetworkScheduleRules ?? Rules(s)) {
                if (rule == null) continue;
                if (rule.KiB <= 0 || rule.KiB > 1048576 || rule.Start < 0 || rule.Start >= 1440 || rule.End < 0 || rule.End >= 1440 || rule.Start == rule.End) continue;
                int minute = now.Hour * 60 + now.Minute, day = ((int)now.DayOfWeek + 6) % 7;
                if (rule.Start < rule.End) { if (minute < rule.Start || minute >= rule.End) continue; }
                else if (minute < rule.End) day = (day + 6) % 7;
                else if (minute < rule.Start) continue;
                if ((rule.Days & (1 << day)) == 0) continue;
                long rate = rule.KiB * 1024; result = result == 0 ? rate : Math.Min(result, rate);
            }
            return result;
        }
    }
    internal sealed class NetworkScheduleEditor : DockPanel
    {
        readonly CheckBox enabled; readonly TabControl rules = new TabControl(); readonly Button add, remove;
        internal NetworkScheduleEditor(ProgramSettings settings)
        {
            Margin = new Thickness(8); var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); Children.Add(top);
            enabled = new CheckBox { Content = L.Text("schedule.enable"), IsChecked = settings.NetworkScheduleEnabled, Margin = new Thickness(0, 4, 0, 8) }; top.Children.Add(enabled);
            var hint = new TextBlock { Text = L.Text("schedule.multiHelp"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted, Margin = new Thickness(0, 0, 0, 8) }; top.Children.Add(hint);
            var actions = new WrapPanel(); top.Children.Add(actions);
            add = CopyUi.Button(L.Text("schedule.add"), delegate { AddRule(new NetworkScheduleRule()); rules.SelectedIndex = rules.Items.Count - 1; }); actions.Children.Add(add);
            remove = CopyUi.Button(L.Text("schedule.remove"), delegate { if (rules.SelectedIndex >= 0) { rules.Items.RemoveAt(rules.SelectedIndex); RefreshNames(); } }); actions.Children.Add(remove);
            rules.Margin = new Thickness(0, 8, 0, 0); Children.Add(rules);
            rules.SelectionChanged += delegate(object sender, SelectionChangedEventArgs e) { if (e.Source == rules) RefreshNames(); };
            foreach (var rule in NetworkSchedule.Rules(settings)) AddRule(rule);
            if (rules.Items.Count > 0) rules.SelectedIndex = 0; RefreshNames();
        }
        void AddRule(NetworkScheduleRule rule) { if (rules.Items.Count >= 4) return; rules.Items.Add(new TabItem { Content = new NetworkScheduleRuleEditor(rule) }); RefreshNames(); }
        void RefreshNames() { for (int i = 0; i < rules.Items.Count; i++) ((TabItem)rules.Items[i]).Header = L.Format("schedule.scenarioNumber", i + 1); add.IsEnabled = rules.Items.Count < 4; remove.IsEnabled = rules.SelectedIndex >= 0; }
        internal void RefreshLanguage() { RefreshNames(); foreach (TabItem tab in rules.Items) ((NetworkScheduleRuleEditor)tab.Content).RefreshLanguage(); }
        internal void Apply(ProgramSettings settings)
        {
            var result = new List<NetworkScheduleRule>();
            foreach (TabItem tab in rules.Items) { try { result.Add(((NetworkScheduleRuleEditor)tab.Content).Read()); } catch { if (enabled.IsChecked == true) { rules.SelectedItem = tab; throw; } settings.NetworkScheduleEnabled = false; return; } }
            settings.NetworkScheduleRules = result; settings.NetworkScheduleEnabled = enabled.IsChecked == true;
        }
    }
    internal sealed class NetworkScheduleRuleEditor : StackPanel
    {
        readonly CheckBox[] days = new CheckBox[7]; readonly TextBox start, end, speed; readonly TextBlock timeline; readonly Grid chart = new Grid { Height = 14, Margin = new Thickness(0, 10, 0, 0) };
        internal NetworkScheduleRuleEditor(NetworkScheduleRule rule)
        {
            Margin = new Thickness(8); var weekdays = new WrapPanel(); Children.Add(weekdays);
            for (int i = 0; i < 7; i++) { days[i] = new CheckBox { Content = CultureInfo.GetCultureInfo(L.Language).DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)((i + 1) % 7)), IsChecked = (rule.Days & (1 << i)) != 0, Margin = new Thickness(0, 4, 14, 8) }; weekdays.Children.Add(days[i]); }
            var row = new WrapPanel(); Children.Add(row); start = Field(row, L.Text("schedule.from"), Format(rule.Start), 62); end = Field(row, L.Text("schedule.to"), Format(rule.End), 62); speed = Field(row, L.Text("schedule.rate"), rule.KiB.ToString(CultureInfo.InvariantCulture), 90);
            Children.Add(chart); timeline = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 4), Foreground = FluentTheme.Muted }; Children.Add(timeline);
            timeline.ToolTip = L.Text("schedule.help");
            start.TextChanged += delegate { UpdatePreview(); }; end.TextChanged += delegate { UpdatePreview(); }; UpdatePreview();
        }
        static TextBox Field(Panel panel, string label, string value, double width) { var group = new StackPanel { Margin = new Thickness(0, 0, 18, 0) }; group.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 4) }); var field = new TextBox { Text = value, Width = width, Height = 30, VerticalContentAlignment = VerticalAlignment.Center }; group.Children.Add(field); panel.Children.Add(group); return field; }
        static string Format(int minute) { return String.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", minute / 60, minute % 60); }
        static bool Parse(string value, out int minutes) { TimeSpan t; bool ok = TimeSpan.TryParseExact(value.Trim(), new[] { @"h\:mm", @"hh\:mm" }, CultureInfo.InvariantCulture, out t) && t >= TimeSpan.Zero && t < TimeSpan.FromDays(1); minutes = ok ? (int)t.TotalMinutes : 0; return ok; }
        void UpdatePreview() {
            int a = 0, b = 0; bool valid = Parse(start.Text, out a) && Parse(end.Text, out b) && a != b;
            timeline.Text = valid ? "00:00     " + L.Text("schedule.preview") + "  " + Format(a) + " → " + Format(b) + (b < a ? "  (+1)" : "") + "     24:00" : L.Text("schedule.invalid");
            chart.Children.Clear(); chart.ColumnDefinitions.Clear(); int[] points = valid ? new[] { 0, Math.Min(a, b), Math.Max(a, b), 1440 } : new[] { 0, 1440 };
            for (int i = 0; i + 1 < points.Length; i++) { int from = points[i], to = points[i + 1]; if (to == from) continue; chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(to - from, GridUnitType.Star) }); bool limited = valid && (a < b ? from >= a && from < b : from >= a || from < b); var segment = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(limited ? "#3486D7" : "#DFE8F2")) }; Grid.SetColumn(segment, chart.ColumnDefinitions.Count - 1); chart.Children.Add(segment); }
        }
        internal void RefreshLanguage() { for (int i = 0; i < 7; i++) days[i].Content = CultureInfo.GetCultureInfo(L.Language).DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)((i + 1) % 7)); UpdatePreview(); }
        internal NetworkScheduleRule Read() {
            int a = 0, b = 0, mask = 0; long value; for (int i = 0; i < 7; i++) if (days[i].IsChecked == true) mask |= 1 << i;
            if (!Parse(start.Text, out a) || !Parse(end.Text, out b) || a == b || mask == 0 || !Int64.TryParse(speed.Text, out value) || value < 1 || value > 1048576) throw new ArgumentException(L.Text("schedule.invalid"));
            return new NetworkScheduleRule { Days = mask, Start = a, End = b, KiB = value };
        }
    }
}

using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace CommanderCZ {
    internal sealed class CloudQuestionDialog : Window {
        readonly CloudOption option;
        readonly StackPanel answers = new StackPanel();
        readonly ComboBox combo;
        readonly PasswordBox secret;
        internal CloudQuestionDialog(Window owner, CloudQuestion question) {
            option = question.Option;
            CopyUi.Style(this, owner, L.Text("cloud.chooseStorage"), 620);
            MaxWidth = DialogGeometry.WorkArea(this).Width - 32; MaxHeight = DialogGeometry.WorkArea(this).Height - 32;
            Width = Math.Min(620, MaxWidth); ResizeMode = ResizeMode.CanResize;
            var body = new DockPanel { Margin = new Thickness(14) }; Content = body;
            var actions = FluentTheme.Actions(); DockPanel.SetDock(actions, Dock.Bottom); body.Children.Add(actions);
            var error = new TextBlock { Foreground = CompactTheme.ThemeBrush("#B92331"), TextWrapping = TextWrapping.Wrap };
            var ok = CopyUi.Button(L.Text("action.resume"), delegate { if (ReadAnswer() == null) { error.Text = L.Text("cloud.selectAnswer"); return; } DialogResult = true; }); ok.IsDefault = true; actions.Children.Add(ok);
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); cancel.IsCancel = true; actions.Children.Add(cancel);
            var stack = new StackPanel(); body.Children.Add(new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            bool shared = option.Name == "config_change_team_drive";
            bool clientWarning = option.Name == "config_shared_client_id";
            bool changing = shared && (option.Help ?? "").StartsWith("Change current", StringComparison.OrdinalIgnoreCase);
            string heading = shared ? L.Text(changing ? "cloud.changeSharedQuestion" : "cloud.sharedQuestion") : option.Name == "config_team_drive" ? L.Text("cloud.selectSharedQuestion") : option.Help;
            string explanation = shared ? L.Text(changing ? "cloud.changeSharedHelp" : "cloud.sharedHelp") : null;
            if (clientWarning) {
                heading = L.Text("cloud.sharedClientQuestion");
                var year = System.Text.RegularExpressions.Regex.Match(option.Help ?? "", @"during\s+(\d{4})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                explanation = year.Success ? L.Format("cloud.sharedClientHelp", year.Groups[1].Value) : L.Text("cloud.sharedClientHelpUndated");
            } else if (option.Name == "client_id") { heading = L.Text("cloud.clientIdQuestion"); explanation = L.Text("cloud.clientIdHelp"); }
            else if (option.Name == "client_secret") { heading = L.Text("cloud.clientSecretQuestion"); explanation = L.Text("cloud.clientSecretHelp"); }
            stack.Children.Add(new TextBlock { Text = String.IsNullOrWhiteSpace(heading) ? L.Text("cloud.questionFallback") : heading.Trim(), TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
            if (explanation != null) stack.Children.Add(new TextBlock { Text = explanation, TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted, Margin = new Thickness(0, 0, 0, 12) });
            if (!String.IsNullOrWhiteSpace(question.Error)) stack.Children.Add(new TextBlock { Text = question.Error, TextWrapping = TextWrapping.Wrap, Foreground = CompactTheme.ThemeBrush("#B92331") });
            var choices = option.Examples ?? new CloudExample[0];
            if (choices.Length == 0 && option.Default is bool) choices = new[] { new CloudExample { Value = "true", Help = "Yes" }, new CloudExample { Value = "false", Help = "No" } };
            string initial = option.Default is bool ? ((bool)option.Default ? "true" : "false") : Convert.ToString(option.Default, CultureInfo.InvariantCulture);
            bool yesNo = choices.Length == 2 && choices.All(x => x.Value == "true" || x.Value == "false");
            if (option.IsPassword || option.Name == "client_secret") { secret = new PasswordBox(); stack.Children.Add(secret); }
            else if (yesNo) {
                foreach (var choice in shared && !changing ? choices.OrderBy(x => x.Value == "false" ? 0 : 1) : choices.AsEnumerable()) {
                    bool yes = choice.Value == "true";
                    string label = shared ? L.Text(changing ? (yes ? "cloud.changeSharedYes" : "cloud.changeSharedNo") : (yes ? "cloud.sharedYes" : "cloud.sharedNo")) : L.Text(yes ? "cloud.answerYes" : "cloud.answerNo");
                    if (clientWarning) label = L.Text(yes ? "cloud.sharedClientYes" : "cloud.sharedClientNo");
                    var radio = new RadioButton { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }, Tag = choice.Value, IsChecked = String.Equals(choice.Value, initial, StringComparison.OrdinalIgnoreCase), Margin = new Thickness(0, 5, 0, 5) };
                    answers.Children.Add(radio);
                }
                stack.Children.Add(answers);
            } else {
                combo = new ComboBox { ItemsSource = choices, IsEditable = !option.Exclusive, Margin = new Thickness(0, 4, 0, 8) };
                combo.SelectedItem = choices.FirstOrDefault(x => x.Value == initial);
                if (combo.SelectedItem == null && combo.IsEditable) combo.Text = initial;
                stack.Children.Add(combo);
            }
            stack.Children.Add(error);
            if ((shared || clientWarning || option.Name == "client_id" || option.Name == "client_secret") && !String.IsNullOrWhiteSpace(option.Help)) {
                var original = new Expander { Header = L.Text("cloud.originalQuestion"), Content = new TextBlock { Text = option.Help.Trim(), TextWrapping = TextWrapping.Wrap }, Margin = new Thickness(0, 12, 0, 0) };
                original.Expanded += delegate { Dispatcher.BeginInvoke(new Action(delegate { FitQuestion(stack); })); };
                original.Collapsed += delegate { Dispatcher.BeginInvoke(new Action(delegate { FitQuestion(stack); })); };
                stack.Children.Add(original);
            }
            Loaded += delegate { FitQuestion(stack); };
        }
        void FitQuestion(StackPanel stack) {
            UpdateLayout(); var scroll = (ScrollViewer)stack.Parent; stack.Measure(new Size(Math.Max(100, scroll.ActualWidth - 18), Double.PositiveInfinity));
            Height = Math.Min(MaxHeight, Math.Max(220, stack.DesiredSize.Height + ActualHeight - scroll.ActualHeight + 8)); SizeToContent = SizeToContent.Manual;
        }
        internal string ReadAnswer() {
            if (secret != null) return option.Required && secret.Password.Length == 0 ? null : secret.Password;
            if (combo != null) { var choice = combo.SelectedItem as CloudExample; if (choice != null) return choice.Value; if (!combo.IsEditable) return null; return option.Required && String.IsNullOrWhiteSpace(combo.Text) ? null : combo.Text; }
            var selected = answers.Children.OfType<RadioButton>().FirstOrDefault(x => x.IsChecked == true); return selected == null ? null : (string)selected.Tag;
        }
    }
}

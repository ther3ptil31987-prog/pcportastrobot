using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AstroHybrid.App.Services;

namespace AstroHybrid.App.Views
{
    public sealed class ShaderPrepWindow : Window
    {
        private enum Outcome { Done, PlayNow, Closed }

        private readonly LauncherService _launcher;
        private readonly Dictionary<string, string> _variables;
        private readonly CancellationTokenSource _stop = new();
        private readonly Stopwatch _clock = new();
        private readonly DispatcherTimer _timer;
        private readonly TextBlock _status;
        private readonly TextBlock _elapsed;
        private Outcome _outcome = Outcome.Closed;
        private bool _finished;

        private ShaderPrepWindow(LauncherService launcher, Dictionary<string, string> variables)
        {
            _launcher = launcher;
            _variables = variables;
            Title = "AstroHybrid - Preparing Shaders";
            Width = 480;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (TryFindResource("BackgroundBrush") is System.Windows.Media.Brush bg) Background = bg;

            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            panel.Children.Add(Text("Preparing shaders for first launch", "TitleTextStyle", new Thickness(0, 0, 0, 8)));
            panel.Children.Add(Text("Building the Vulkan shader cache once so levels load without first-time compilation stutter.", "BodyTextStyle", new Thickness(0, 0, 0, 14)));
            var bar = new ProgressBar { Height = 10, IsIndeterminate = true };
            if (TryFindResource("ModernProgressBarStyle") is Style barStyle) bar.Style = barStyle;
            panel.Children.Add(bar);
            _status = Text("Starting shader preparation...", "BodyTextStyle", new Thickness(0, 12, 0, 0));
            panel.Children.Add(_status);
            _elapsed = Text("", "MutedTextStyle", new Thickness(0, 4, 0, 0));
            panel.Children.Add(_elapsed);
            panel.Children.Add(Text("You can skip this and compile shaders while playing, though the first run of each level may stutter.", "MutedTextStyle", new Thickness(0, 20, 0, 10)));
            var playNow = new Button { Content = "Play now", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 180, Padding = new Thickness(16, 8, 16, 8) };
            if (TryFindResource("SecondaryButtonStyle") is Style buttonStyle) playNow.Style = buttonStyle;
            playNow.Click += (s, e) => { _outcome = Outcome.PlayNow; _stop.Cancel(); };
            panel.Children.Add(playNow);
            Content = panel;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => _elapsed.Text = $"Elapsed: {(int)_clock.Elapsed.TotalSeconds}s";
            Loaded += async (s, e) => await RunAsync();
            Closing += (s, e) => { if (!_finished) _stop.Cancel(); };
        }

        private TextBlock Text(string text, string style, Thickness margin)
        {
            var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = margin };
            if (TryFindResource(style) is Style found) block.Style = found;
            return block;
        }

        private async Task RunAsync()
        {
            _clock.Start();
            _timer.Start();
            var status = new Progress<string>(text => _status.Text = text);
            try { await _launcher.PrepareShadersAsync(_variables, status, _stop.Token); }
            catch { }
            _timer.Stop();
            _finished = true;
            if (!_stop.IsCancellationRequested) _outcome = Outcome.Done;
            Close();
        }

        public static bool PrepareBeforePlay(LauncherService launcher, Dictionary<string, string> variables)
        {
            var app = Application.Current;
            var window = new ShaderPrepWindow(launcher, variables);
            var owner = app?.MainWindow;
            if (owner != null && owner != window && owner.IsVisible) window.Owner = owner;
            var mode = app?.ShutdownMode ?? ShutdownMode.OnExplicitShutdown;
            if (app != null) app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try { window.ShowDialog(); }
            finally { if (app != null) app.ShutdownMode = mode; }
            return window._outcome != Outcome.Closed;
        }
    }
}

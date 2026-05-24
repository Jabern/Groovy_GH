// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

using Groovy_GH.Core;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Point = System.Windows.Point;

namespace Groovy_GH
{
    public class Groovy_SettingsForm : Window
    {
        private WriteableBitmap? _waveformBitmap;
        private int _waveformWidth, _waveformHeight;
        private bool _mouseDownOnWaveform;

        private ComboBox _presetCombo = null!;
        private Button _playBtn = null!, _pauseBtn = null!, _stopBtn = null!;

        private Canvas _waveCanvas = null!;
        private Line _playheadLine = null!;
        private Rectangle _bassBar = null!, _midBar = null!, _trebleBar = null!;
        private Ellipse _beatDot = null!;
        private TextBlock _timeLabel = null!;

        private Slider _bassRange = null!, _midRange = null!, _trebleRange = null!;
        private ComboBox _bassCurve = null!, _midCurve = null!, _trebleCurve = null!;
        private TextBox _bassExp = null!, _midExp = null!, _trebleExp = null!;
        private CheckBox _bassBeat = null!, _midBeat = null!, _trebleBeat = null!;

        private CheckBox _beatEn = null!;
        private Slider _beatThr = null!, _beatMag = null!;
        private TextBox _beatAtt = null!, _beatDec = null!;

        private CheckBox _smoothEn = null!;
        private Slider _smoothWin = null!;

        private TextBox _fpsBox = null!, _panelsBox = null!;

        private DispatcherTimer _visTimer = null!;
        private bool _dirty;
        private bool _beatDotLit;
        private int _debounceCount;

        private static readonly string[] PresetNames = { "EDM", "Techno", "Classical", "Rock" };
        private static readonly string[] PresetKeys = { "edm", "techno", "classical", "rock" };
        private static readonly string[] CurveNames = { "Linear", "Exponential", "Sqrt" };

        public Groovy_SettingsForm()
        {
            Title = "Groovy";
            Width = 440; Height = 680; MinWidth = 380; MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Content = scroll;

            var root = new StackPanel { Margin = new Thickness(10) };
            scroll.Content = root;

            BuildHeader(root);
            BuildVis(root);
            BuildBandControls(root);
            BuildBeatControls(root);
            BuildSmoothControls(root);
            BuildGeneral(root);

            Loaded += (s, e) => { DrawWaveformBitmap(); ApplyConfigToUi(); };
            Closed += (s, e) => { _visTimer?.Stop(); GroovyState.StopPlayback(); };
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };

            _visTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _visTimer.Tick += OnVisTick;
            _visTimer.Start();
        }

        private void BuildHeader(StackPanel root)
        {
            var p = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };

            p.Children.Add(new TextBlock { Text = "Preset:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            _presetCombo = new ComboBox { Width = 100, Height = 24 };
            foreach (var n in PresetNames) _presetCombo.Items.Add(n);
            _presetCombo.SelectedIndex = 0;
            _presetCombo.SelectionChanged += (s, e) => LoadPreset(_presetCombo.SelectedIndex);
            p.Children.Add(_presetCombo);

            _playBtn = new Button { Content = "[>]", Width = 32, Height = 24, Margin = new Thickness(8, 0, 2, 0) };
            _playBtn.Click += (s, e) =>
            {
                if (GroovyState.PlayAction == null)
                {
                    System.Windows.MessageBox.Show(
                        "No Groovy component found.\n" +
                        "Place and solve the Groovy component first.");
                    return;
                }
                // flush pending slider changes before playing or they get ignored on the first frame
                if (_dirty) SaveConfig();
                GroovyState.PlayAction();
            };
            p.Children.Add(_playBtn);

            _pauseBtn = new Button { Content = "||", Width = 32, Height = 24, Margin = new Thickness(2, 0, 2, 0) };
            _pauseBtn.Click += (s, e) => GroovyState.Pause();
            p.Children.Add(_pauseBtn);

            _stopBtn = new Button { Content = "[X]", Width = 32, Height = 24, Margin = new Thickness(2, 0, 0, 0) };
            _stopBtn.Click += (s, e) => GroovyState.StopAction?.Invoke();
            p.Children.Add(_stopBtn);

            _timeLabel = new TextBlock { Text = "0:00 / 0:00", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), FontSize = 12 };
            p.Children.Add(_timeLabel);

            root.Children.Add(p);
        }

        private void BuildVis(StackPanel root)
        {
            var g = new GroupBox { Header = "Visualizer", Height = 140, Margin = new Thickness(0, 0, 0, 6) };
            _waveCanvas = new Canvas { Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)), ClipToBounds = true };
            _waveCanvas.MouseLeftButtonDown += OnWaveMouseDown;
            _waveCanvas.MouseMove += OnWaveMouseMove;
            _waveCanvas.MouseLeftButtonUp += (s, e) =>
            {
                _mouseDownOnWaveform = false;
                _waveCanvas.ReleaseMouseCapture();
            };
            _waveCanvas.LostMouseCapture += (s, e) =>
                _mouseDownOnWaveform = false;
            _waveCanvas.SizeChanged += (s, e) => DrawWaveformBitmap();

            MouseLeftButtonUp += (s, e) =>
            {
                _mouseDownOnWaveform = false;
                _waveCanvas.ReleaseMouseCapture();
            };

            _playheadLine = new Line { Stroke = Brushes.Red, StrokeThickness = 2, Y1 = 0 };
            _waveCanvas.Children.Add(_playheadLine);

            string[] labels = { "BASS", "MID", "TREB" };
            var colors = new[] { Brushes.DarkRed, Brushes.DarkOrange, Brushes.DarkCyan };
            for (int i = 0; i < 3; i++)
            {
                var bar = new Rectangle { Fill = colors[i], Width = 28, Height = 2 };
                Canvas.SetBottom(bar, 24);
                Canvas.SetLeft(bar, 8 + i * 36);
                _waveCanvas.Children.Add(bar);
                if (i == 0) _bassBar = bar;
                else if (i == 1) _midBar = bar;
                else _trebleBar = bar;

                var lbl = new TextBlock { Text = labels[i], Foreground = Brushes.Gray, FontSize = 8, Width = 28, TextAlignment = TextAlignment.Center };
                Canvas.SetBottom(lbl, 22);
                Canvas.SetLeft(lbl, 8 + i * 36);
                _waveCanvas.Children.Add(lbl);
            }

            _beatDot = new Ellipse { Fill = Brushes.Yellow, Width = 10, Height = 10, Opacity = 0 };
            Canvas.SetRight(_beatDot, 6);
            Canvas.SetTop(_beatDot, 6);
            _waveCanvas.Children.Add(_beatDot);

            g.Content = _waveCanvas;
            root.Children.Add(g);
        }

        private void OnWaveMouseDown(object sender, MouseButtonEventArgs e)
        {
            _mouseDownOnWaveform = true;
            SeekFromMouse(e.GetPosition(_waveCanvas));
            // horrible hack: capture the mouse for scrubbing or clicks fall through to the canvas below
            _waveCanvas.CaptureMouse();
        }

        private void OnWaveMouseMove(object sender, MouseEventArgs e)
        {
            if (!_mouseDownOnWaveform) return;
            SeekFromMouse(e.GetPosition(_waveCanvas));
        }

        private void SeekFromMouse(Point pt)
        {
            var analysis = GroovyState.Analysis;
            if (analysis == null || analysis.Duration <= 0) return;
            double frac = MathUtils.Clamp(pt.X / Math.Max(1, _waveCanvas.ActualWidth), 0, 1);
            double sec = frac * analysis.Duration;
            GroovyState.SeekTo(sec);
            GroovyState.ExpireAction?.Invoke();
        }

        private void DrawWaveformBitmap()
        {
            int w = (int)_waveCanvas.ActualWidth;
            int h = (int)_waveCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;
            if (w == _waveformWidth && h == _waveformHeight && _waveformBitmap != null) return;
            _waveformWidth = w; _waveformHeight = h;

            _waveformBitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            var pixels = new byte[w * h * 4];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = (byte)((i % 4) == 3 ? 255 : 24);

            var analysis = GroovyState.Analysis;
            if (analysis != null && analysis.Frames.Count > 1)
            {
                int step = Math.Max(1, analysis.Frames.Count / w);
                double mid = h * 0.45;
                for (int x = 0; x < w && x * step < analysis.Frames.Count; x++)
                {
                    double rms = analysis.Frames[x * step].Rms;
                    int y = (int)(mid - rms * mid * 0.85);
                    y = MathUtils.Clamp(y, 2, h - 2);
                    int idx = (y * w + x) * 4;
                    pixels[idx] = 0; pixels[idx + 1] = 170; pixels[idx + 2] = 80;
                }
            }

            _waveformBitmap.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);

            var img = _waveCanvas.Children.OfType<Image>().FirstOrDefault();
            if (img == null) { img = new Image(); _waveCanvas.Children.Insert(0, img); }
            img.Source = _waveformBitmap;
            img.Width = w; img.Height = h;
        }

        private void OnVisTick(object? sender, EventArgs e)
        {
            if (_debounceCount > 0)
            {
                _debounceCount--;
                if (_debounceCount == 0 && _dirty) SaveConfig();
            }

            var analysis = GroovyState.Analysis;
            double time = GroovyState.GetTimeSeconds();
            bool playing = GroovyState.IsPlaying;

            if (playing && !GroovyState.IsPaused)
            {
                _playBtn.Content = "[>]";
                _pauseBtn.Content = "||";
            }
            else if (playing && GroovyState.IsPaused)
            {
                _playBtn.Content = "[>]";
                _pauseBtn.Content = "[>]";
            }
            else
            {
                _playBtn.Content = "[>]";
                _pauseBtn.Content = "||";
            }

            if (analysis != null && analysis.Frames.Count > 0)
            {
                double frac = time / Math.Max(0.001, analysis.Duration);
                double x = _waveCanvas.ActualWidth * frac;
                _playheadLine.X1 = _playheadLine.X2 = x;
                _playheadLine.Y2 = _waveCanvas.ActualHeight;

                var frame = analysis.GetFrameAtTime(time);
                double maxH = _waveCanvas.ActualHeight * 0.35;
                _bassBar.Height = Math.Max(1, frame.Bands.Length > 0 ? frame.Bands[0] * maxH : 0);
                _midBar.Height = Math.Max(1, frame.Bands.Length > 1 ? frame.Bands[1] * maxH : 0);
                _trebleBar.Height = Math.Max(1, frame.Bands.Length > 2 ? frame.Bands[2] * maxH : 0);

                if (frame.IsBeat > 0.5 && !_beatDotLit)
                {
                    _beatDotLit = true;
                    _beatDot.Opacity = 1.0;
                    _beatDot.BeginAnimation(OpacityProperty,
                        new System.Windows.Media.Animation.DoubleAnimation(
                            0, TimeSpan.FromMilliseconds(250)));
                }
                else if (frame.IsBeat <= 0.5)
                {
                    _beatDotLit = false;
                }

                int tMin = (int)(time / 60);
                int tSec = (int)(time % 60);
                int dMin = (int)(analysis.Duration / 60);
                int dSec = (int)(analysis.Duration % 60);
                _timeLabel.Text = $"{tMin}:{tSec:D2} / {dMin}:{dSec:D2}";
            }
        }

        private void BuildBandControls(StackPanel root)
        {
            var exp = new Expander { Header = "Panel Groups ▼", IsExpanded = true, Margin = new Thickness(0, 0, 0, 4) };
            var grid = new Grid { Margin = new Thickness(4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });

            AddLabel(grid, 0, "", "Range mm", "Curve", "Exp", "Beat");
            _bassRange = MakeRow(grid, 1, "Bass", 600, out _bassCurve, out _bassExp, out _bassBeat);
            _midRange = MakeRow(grid, 2, "Mid", 400, out _midCurve, out _midExp, out _midBeat);
            _trebleRange = MakeRow(grid, 3, "Treb", 200, out _trebleCurve, out _trebleExp, out _trebleBeat);

            exp.Content = grid;
            root.Children.Add(exp);
        }

        private Slider MakeRow(Grid grid, int row, string label, double defVal,
            out ComboBox curve, out TextBox expBox, out CheckBox beat)
        {
            AddLabel(grid, row, label, "", "", "", "");

            var s = new Slider { Minimum = 10, Maximum = 2000, Value = defVal, Height = 20, VerticalAlignment = VerticalAlignment.Center };
            s.ValueChanged += (_, _) => MarkDirty();
            Grid.SetRow(s, row); Grid.SetColumn(s, 1); grid.Children.Add(s);

            curve = new ComboBox { Height = 22, VerticalAlignment = VerticalAlignment.Center };
            foreach (var n in CurveNames) curve.Items.Add(n);
            curve.SelectedIndex = 0;
            curve.SelectionChanged += (_, _) => MarkDirty();
            Grid.SetRow(curve, row); Grid.SetColumn(curve, 2); grid.Children.Add(curve);

            expBox = new TextBox { Text = "1.5", Width = 32, Height = 22, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center };
            expBox.TextChanged += (_, _) => MarkDirty();
            Grid.SetRow(expBox, row); Grid.SetColumn(expBox, 3); grid.Children.Add(expBox);

            beat = new CheckBox { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            beat.Checked += (_, _) => MarkDirty();
            beat.Unchecked += (_, _) => MarkDirty();
            Grid.SetRow(beat, row); Grid.SetColumn(beat, 4); grid.Children.Add(beat);

            return s;
        }

        private void BuildBeatControls(StackPanel root)
        {
            var exp = new Expander { Header = "Beat Impulse ▼", IsExpanded = true, Margin = new Thickness(0, 0, 0, 4) };
            var p = new WrapPanel { Margin = new Thickness(4) };

            _beatEn = new CheckBox { Content = "On", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
            _beatEn.Checked += (_, _) => MarkDirty(); _beatEn.Unchecked += (_, _) => MarkDirty();
            p.Children.Add(_beatEn);

            p.Children.Add(new TextBlock { Text = " Thr:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 2, 0) });
            _beatThr = new Slider { Minimum = 0.1, Maximum = 1.0, Value = 0.5, Width = 80, Height = 20 };
            _beatThr.ValueChanged += (_, _) => MarkDirty();
            p.Children.Add(_beatThr);

            p.Children.Add(new TextBlock { Text = " Mag:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 2, 0) });
            _beatMag = new Slider { Minimum = 50, Maximum = 1000, Value = 300, Width = 80, Height = 20 };
            _beatMag.ValueChanged += (_, _) => MarkDirty();
            p.Children.Add(_beatMag);

            p.Children.Add(new TextBlock { Text = " Att:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 2, 0) });
            _beatAtt = new TextBox { Text = "2", Width = 30, Height = 20, TextAlignment = TextAlignment.Center };
            _beatAtt.TextChanged += (_, _) => MarkDirty();
            p.Children.Add(_beatAtt);

            p.Children.Add(new TextBlock { Text = " Dec:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0) });
            _beatDec = new TextBox { Text = "8", Width = 30, Height = 20, TextAlignment = TextAlignment.Center };
            _beatDec.TextChanged += (_, _) => MarkDirty();
            p.Children.Add(_beatDec);

            exp.Content = p;
            root.Children.Add(exp);
        }

        private void BuildSmoothControls(StackPanel root)
        {
            var exp = new Expander { Header = "Smoothing ▼", IsExpanded = true, Margin = new Thickness(0, 0, 0, 4) };
            var p = new WrapPanel { Margin = new Thickness(4) };

            _smoothEn = new CheckBox { Content = "On", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
            _smoothEn.Checked += (_, _) => MarkDirty(); _smoothEn.Unchecked += (_, _) => MarkDirty();
            p.Children.Add(_smoothEn);

            p.Children.Add(new TextBlock { Text = " Win:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 2, 0) });
            _smoothWin = new Slider { Minimum = 1, Maximum = 20, Value = 3, Width = 100, Height = 20, TickFrequency = 1, IsSnapToTickEnabled = true };
            _smoothWin.ValueChanged += (_, _) => MarkDirty();
            p.Children.Add(_smoothWin);

            exp.Content = p;
            root.Children.Add(exp);
        }

        private void BuildGeneral(StackPanel root)
        {
            var exp = new Expander { Header = "General ▼", IsExpanded = true, Margin = new Thickness(0, 0, 0, 4) };
            var p = new WrapPanel { Margin = new Thickness(4) };

            p.Children.Add(new TextBlock { Text = "FPS:", VerticalAlignment = VerticalAlignment.Center });
            _fpsBox = new TextBox { Text = "30", Width = 36, Height = 20, TextAlignment = TextAlignment.Center, Margin = new Thickness(4, 0, 10, 0) };
            _fpsBox.TextChanged += (_, _) => MarkDirty();
            p.Children.Add(_fpsBox);

            p.Children.Add(new TextBlock { Text = "Panels:", VerticalAlignment = VerticalAlignment.Center });
            _panelsBox = new TextBox { Text = "100", Width = 40, Height = 20, TextAlignment = TextAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            _panelsBox.TextChanged += (_, _) => MarkDirty();
            p.Children.Add(_panelsBox);

            exp.Content = p;
            root.Children.Add(exp);
        }

// debounce: wait 6 ticks before saving, otherwise it spams expire solution on every drag
        private void MarkDirty() { _dirty = true; _debounceCount = 6; }

        private void SaveConfig()
        {
            if (!_dirty) return; _dirty = false;

            var cfg = GroovyState.ActiveConfig;

            if (int.TryParse(_fpsBox.Text, out int fps) && fps > 0)
                cfg.Fps = MathUtils.Clamp(fps, 1, 120);
            if (int.TryParse(_panelsBox.Text, out int pn) && pn > 0)
                cfg.NumPanels = pn;

            cfg.BeatImpulse.Enabled = _beatEn.IsChecked == true;
            cfg.BeatImpulse.Threshold = _beatThr.Value;
            cfg.BeatImpulse.MagnitudeMm = _beatMag.Value;
            if (int.TryParse(_beatAtt.Text, out int att) && att > 0) cfg.BeatImpulse.AttackFrames = att;
            if (int.TryParse(_beatDec.Text, out int dec) && dec > 0) cfg.BeatImpulse.DecayFrames = dec;

            cfg.Smoothing.Enabled = _smoothEn.IsChecked == true;
            cfg.Smoothing.WindowSize = (int)_smoothWin.Value;

            var sliders = new[] { _bassRange, _midRange, _trebleRange };
            var curves = new[] { _bassCurve, _midCurve, _trebleCurve };
            var exps = new[] { _bassExp, _midExp, _trebleExp };
            var beats = new[] { _bassBeat, _midBeat, _trebleBeat };

            while (cfg.PanelGroups.Count < 3)
                cfg.PanelGroups.Add(new PanelGroupConfig());

            for (int i = 0; i < 3 && i < cfg.PanelGroups.Count; i++)
            {
                var g = cfg.PanelGroups[i];
                g.RangeMm = new[] { 0.0, sliders[i].Value };
                g.ResponseCurveStr = curves[i].SelectedIndex switch { 1 => "exponential", 2 => "sqrt", _ => "linear" };
                if (double.TryParse(exps[i].Text, out double ex) && ex > 0) g.Exponent = ex;
                g.BeatEnabled = beats[i].IsChecked == true;
            }

            cfg.IncrementVersion();
            GroovyState.ExpireAction?.Invoke();
        }

        private void ApplyConfigToUi()
        {
            var cfg = GroovyState.ActiveConfig;
            _fpsBox.Text = cfg.Fps.ToString();
            _panelsBox.Text = cfg.NumPanels.ToString();

            _beatEn.IsChecked = cfg.BeatImpulse.Enabled;
            _beatThr.Value = cfg.BeatImpulse.Threshold;
            _beatMag.Value = cfg.BeatImpulse.MagnitudeMm;
            _beatAtt.Text = cfg.BeatImpulse.AttackFrames.ToString();
            _beatDec.Text = cfg.BeatImpulse.DecayFrames.ToString();

            _smoothEn.IsChecked = cfg.Smoothing.Enabled;
            _smoothWin.Value = cfg.Smoothing.WindowSize;

            var sliders = new[] { _bassRange, _midRange, _trebleRange };
            var curves = new[] { _bassCurve, _midCurve, _trebleCurve };
            var exps = new[] { _bassExp, _midExp, _trebleExp };
            var beats = new[] { _bassBeat, _midBeat, _trebleBeat };

            for (int i = 0; i < cfg.PanelGroups.Count && i < 3; i++)
            {
                var g = cfg.PanelGroups[i];
                double r = g.RangeMm.Length > 1 ? g.RangeMm[1] : 400;
                sliders[i].Value = MathUtils.Clamp(r, 10, 2000);
                curves[i].SelectedIndex = g.ResponseCurveStr?.ToLowerInvariant() switch
                {
                    "exponential" => 1, "sqrt" => 2, _ => 0
                };
                exps[i].Text = g.Exponent.ToString("F1");
                beats[i].IsChecked = g.BeatEnabled;
            }

            _dirty = false;
        }

        private void LoadPreset(int idx)
        {
            if (idx < 0 || idx >= PresetKeys.Length) return;
            string key = PresetKeys[idx];
            string json = "";
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                string rn = $"Groovy_GH.Presets.{key}.json";
                using var s = asm.GetManifestResourceStream(rn);
                if (s != null)
                {
                    using var r = new System.IO.StreamReader(s);
                    json = r.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                GroovyState.LastError = $"Preset load failed: {ex.Message}";
                return;
            }

            if (!string.IsNullOrEmpty(json))
            {
                try { GroovyState.SetConfig(MotionMappingConfig.FromJson(json)); }
                catch (Exception ex)
                {
                    GroovyState.LastError = $"Preset parse failed: {ex.Message}";
                }
            }

            ApplyConfigToUi();
            GroovyState.ExpireAction?.Invoke();
        }

        private static void AddLabel(Grid grid, int row, string c0, string c1, string c2, string c3, string c4)
        {
            var texts = new[] { c0, c1, c2, c3, c4 };
            for (int i = 0; i < 5; i++)
            {
                var tb = new TextBlock { Text = texts[i], VerticalAlignment = VerticalAlignment.Center, FontSize = 10, FontWeight = row == 0 ? FontWeights.Bold : FontWeights.Normal };
                Grid.SetRow(tb, row); Grid.SetColumn(tb, i); grid.Children.Add(tb);
            }
        }
    }
}

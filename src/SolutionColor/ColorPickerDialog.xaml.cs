using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;

namespace SolutionColor
{
    internal enum ColorDialogResult
    {
        Cancel,
        Save,
        Remove,
    }

    public partial class ColorPickerDialog : DialogWindow
    {
        private static readonly string[] Presets =
        {
            "#E11D48", "#DC2626", "#EA580C", "#D97706", "#CA8A04", "#65A30D",
            "#16A34A", "#059669", "#0D9488", "#0891B2", "#0284C7", "#2563EB",
            "#4F46E5", "#7C3AED", "#9333EA", "#C026D3", "#DB2777", "#475569",
        };

        private readonly string _solutionName;
        private readonly Action<SolutionColorSettings> _livePreview;
        private readonly ImageSource _icon;
        private bool _loading = true;
        private Color _color;

        internal ColorPickerDialog(string solutionName, SolutionColorSettings current, ImageSource icon, Action<SolutionColorSettings> livePreview)
        {
            InitializeComponent();

            _solutionName = solutionName;
            _livePreview = livePreview;
            _icon = icon;
            Title = $"Solution Color - {solutionName}";
            SharedText.Text = $"Share with the team (save as {solutionName}.slncolor.json next to the solution)";

            var settings = current ?? new SolutionColorSettings { Color = ColorFromName(solutionName) };
            _color = settings.GetColor();
            HexBox.Text = SolutionColorSettings.ToHex(_color);
            TitleBarCheck.IsChecked = settings.ColorTitleBar;
            IconCheck.IsChecked = settings.TintIcon;
            BadgeCheck.IsChecked = settings.TaskbarBadge;
            BorderCheck.IsChecked = settings.ColorWindowBorder;
            SharedCheck.IsChecked = settings.Shared;

            foreach (var hex in Presets)
            {
                SolutionColorSettings.TryParseColor(hex, out var c);
                var swatch = new Button
                {
                    Width = 26,
                    Height = 26,
                    Margin = new Thickness(0, 0, 6, 6),
                    Background = new SolidColorBrush(c),
                    BorderBrush = Brushes.Transparent,
                    ToolTip = hex,
                    Cursor = System.Windows.Input.Cursors.Hand,
                };
                swatch.Click += (s, e) => SetColor(c);
                Swatches.Children.Add(swatch);
            }

            foreach (var cb in new[] { TitleBarCheck, IconCheck, BadgeCheck, BorderCheck })
            {
                cb.Checked += (s, e) => OnChanged();
                cb.Unchecked += (s, e) => OnChanged();
            }

            _loading = false;
            OnChanged();
        }

        internal ColorDialogResult Result { get; private set; } = ColorDialogResult.Cancel;

        internal SolutionColorSettings Settings => new SolutionColorSettings
        {
            Color = SolutionColorSettings.ToHex(_color),
            ColorTitleBar = TitleBarCheck.IsChecked == true,
            TintIcon = IconCheck.IsChecked == true,
            TaskbarBadge = BadgeCheck.IsChecked == true,
            ColorWindowBorder = BorderCheck.IsChecked == true,
            Shared = SharedCheck.IsChecked == true,
        };

        private void SetColor(Color color)
        {
            _color = color;
            HexBox.Text = SolutionColorSettings.ToHex(color); // triggers OnChanged
        }

        private void OnChanged()
        {
            if (_loading)
                return;

            Preview.Background = new SolidColorBrush(_color);
            PreviewText.Text = _solutionName;
            PreviewText.Foreground = new SolidColorBrush(ImageTinter.ContrastingForeground(_color));
            PreviewIcon.Source = _icon != null && IconCheck.IsChecked == true ? ImageTinter.Tint(_icon, _color, 40, 40) : _icon;

            _livePreview?.Invoke(Settings);
        }

        private void HexBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SolutionColorSettings.TryParseColor(HexBox.Text, out var c))
            {
                _color = c;
                HexBox.ClearValue(Control.BorderBrushProperty);
                OnChanged();
            }
            else
            {
                HexBox.BorderBrush = Brushes.Red;
            }
        }

        private void MoreColors_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.ColorDialog
            {
                FullOpen = true,
                AnyColor = true,
                Color = System.Drawing.Color.FromArgb(_color.R, _color.G, _color.B),
            })
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    SetColor(Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B));
            }
        }

        private void FromName_Click(object sender, RoutedEventArgs e)
        {
            SolutionColorSettings.TryParseColor(ColorFromName(_solutionName), out var c);
            SetColor(c);
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            Result = ColorDialogResult.Remove;
            DialogResult = true;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Result = ColorDialogResult.Save;
            DialogResult = true;
        }

        /// <summary>Stable, reasonably saturated color derived from the solution name.</summary>
        internal static string ColorFromName(string name)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char ch in (name ?? string.Empty).ToLowerInvariant())
                    hash = (hash ^ ch) * 16777619;

                double hue = hash % 360;
                var c = HsvToColor(hue, 0.65, 0.75);
                return SolutionColorSettings.ToHex(c);
            }
        }

        private static Color HsvToColor(double h, double s, double v)
        {
            double c = v * s;
            double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
            double m = v - c;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }
    }
}

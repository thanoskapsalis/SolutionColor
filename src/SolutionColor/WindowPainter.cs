using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Microsoft.VisualStudio.Shell;

namespace SolutionColor
{
    /// <summary>
    /// Applies a solution color to the Visual Studio main window: title bar background,
    /// title bar logo, window/taskbar icon, taskbar overlay badge and the DWM window border.
    /// Every change is recorded so it can be undone exactly.
    /// </summary>
    internal sealed class WindowPainter
    {
        private readonly Window _window;
        private readonly Logger _log;
        private readonly List<PropertyOverride> _overrides = new List<PropertyOverride>();

        private SolutionColorSettings _settings;
        private bool _reapplyHooked;

        private Image _logo;
        private Adorner _logoAdorner;

        private bool _iconReplaced;
        private ImageSource _originalIcon;

        private bool _badgeApplied;
        private bool _createdTaskbarInfo;
        private ImageSource _originalOverlay;

        private bool _borderApplied;

        public WindowPainter(Window window, Logger log)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _log = log;
        }

        public void Apply(SolutionColorSettings settings)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _settings = settings;
            Restore();
            if (settings == null)
                return;

            HookReapplyEvents();

            var color = settings.GetColor();
            var titleBar = FindTitleBar();
            if (titleBar == null)
                _log.Write("Could not locate the title bar element in the main window visual tree.");
            else
                _log.Write($"Title bar element: {Describe(titleBar)}");

            if (settings.ColorTitleBar && titleBar != null)
                PaintTitleBar(titleBar, color);

            if (settings.TintIcon)
            {
                if (titleBar != null)
                    TintLogo(titleBar, color);
                TintWindowIcon(color);
            }

            if (settings.TaskbarBadge)
                ApplyBadge(color);

            if (settings.ColorWindowBorder)
                SetBorderColor(color);
        }

        /// <summary>Undo everything this painter changed.</summary>
        public void Restore()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (var o in _overrides)
                o.Restore();
            _overrides.Clear();

            if (_logoAdorner != null)
            {
                AdornerLayer.GetAdornerLayer(_logo)?.Remove(_logoAdorner);
                _logoAdorner = null;
            }
            _logo = null;

            if (_iconReplaced)
            {
                _window.Icon = _originalIcon;
                _iconReplaced = false;
                _originalIcon = null;
            }

            if (_badgeApplied)
            {
                if (_createdTaskbarInfo)
                    _window.TaskbarItemInfo = null;
                else if (_window.TaskbarItemInfo != null)
                    _window.TaskbarItemInfo.Overlay = _originalOverlay;
                _badgeApplied = false;
                _createdTaskbarInfo = false;
                _originalOverlay = null;
            }

            if (_borderApplied)
            {
                SetDwmBorder(DWMWA_COLOR_DEFAULT);
                _borderApplied = false;
            }
        }

        public void Clear()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _settings = null;
            Restore();
        }

        // ------------------------------------------------------------------ title bar

        private FrameworkElement FindTitleBar()
        {
            // 1. Known Visual Studio element (VS 2019 - 2026): Microsoft.VisualStudio.PlatformUI.MainWindowTitleBar
            var byName = Descendants(_window).OfType<FrameworkElement>().FirstOrDefault(fe =>
                fe.GetType().Name == "MainWindowTitleBar" || fe.Name == "MainWindowTitleBar");
            if (byName != null && byName.ActualHeight > 0)
                return byName;

            // 2. Anything that looks like a title bar and sits at the top of the window.
            var candidate = Descendants(_window).OfType<FrameworkElement>().FirstOrDefault(fe =>
                fe.ActualHeight > 0 && fe.ActualHeight < 120 &&
                fe.ActualWidth > _window.ActualWidth * 0.8 &&
                (fe.GetType().Name.EndsWith("TitleBar", StringComparison.OrdinalIgnoreCase) ||
                 fe.Name.IndexOf("TitleBar", StringComparison.OrdinalIgnoreCase) >= 0) &&
                IsAtTop(fe));
            if (candidate != null)
                return candidate;

            // 3. Hit test an (usually empty) spot near the top right of the window and walk up.
            foreach (var x in new[] { 0.72, 0.62, 0.85 })
            {
                var point = new Point(_window.ActualWidth * x, 4);
                var hit = _window.InputHitTest(point) as DependencyObject;
                FrameworkElement best = null;
                for (var d = hit; d != null && d != _window; d = VisualTreeHelper.GetParent(d))
                {
                    if (d is FrameworkElement fe && SupportsBackground(fe) &&
                        fe.ActualWidth > _window.ActualWidth * 0.8 && fe.ActualHeight > 0 && fe.ActualHeight < 120)
                    {
                        best = fe;
                    }
                }
                if (best != null)
                    return best;
            }

            return null;
        }

        private void PaintTitleBar(FrameworkElement titleBar, Color color)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var brush = new SolidColorBrush(color);
            brush.Freeze();

            int painted = 0;
            if (SetBackground(titleBar, brush))
                painted++;

            // Templates frequently draw their own background in an inner Border/Grid that ignores
            // Control.Background. Paint any full-size descendant that has an opaque background too,
            // as well as the main menu (which in VS 2022+ lives inside the title bar).
            foreach (var d in Descendants(titleBar, maxDepth: 12).OfType<FrameworkElement>())
            {
                bool fullSize = d.ActualWidth >= titleBar.ActualWidth * 0.9 && d.ActualHeight >= titleBar.ActualHeight * 0.6;
                bool isMenu = d is Menu;
                if ((fullSize && HasVisibleBackground(d)) || (isMenu && HasVisibleBackground(d)))
                {
                    if (SetBackground(d, brush))
                        painted++;
                }
            }

            _log.Write($"Painted {painted} title bar element(s) with {SolutionColorSettings.ToHex(color)}.");
        }

        private bool SetBackground(FrameworkElement fe, Brush brush)
        {
            var dp = BackgroundProperty(fe);
            if (dp == null)
                return false;

            _overrides.Add(PropertyOverride.Capture(fe, dp));
            fe.SetValue(dp, brush);
            return true;
        }

        // ------------------------------------------------------------------ icons

        private void TintLogo(FrameworkElement titleBar, Color color)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            // The VS logo is the left-most small image in the title bar.
            var logo = Descendants(titleBar, maxDepth: 25)
                .OfType<Image>()
                .Where(i => i.IsVisible && i.Source != null && i.ActualWidth >= 10 && i.ActualWidth <= 48 && i.ActualHeight <= 48)
                .Select(i => new { Image = i, X = SafeX(i, titleBar) })
                .Where(i => i.X < 120)
                .OrderBy(i => i.X)
                .Select(i => i.Image)
                .FirstOrDefault();

            if (logo == null)
            {
                _log.Write("No title bar logo image found to tint.");
                return;
            }

            var layer = AdornerLayer.GetAdornerLayer(logo);
            if (layer == null)
            {
                _log.Write("No adorner layer available for title bar logo.");
                return;
            }

            var dpi = VisualTreeHelper.GetDpi(logo);
            int pw = Math.Max(1, (int)Math.Round(logo.ActualWidth * dpi.DpiScaleX));
            int ph = Math.Max(1, (int)Math.Round(logo.ActualHeight * dpi.DpiScaleY));
            var tinted = ImageTinter.Tint(logo.Source, color, pw, ph);
            if (tinted == null)
                return;

            _logo = logo;
            _overrides.Add(PropertyOverride.Capture(logo, UIElement.OpacityProperty));
            logo.Opacity = 0;
            _logoAdorner = new ImageAdorner(logo, tinted);
            layer.Add(_logoAdorner);
            _log.Write($"Tinted title bar logo: {Describe(logo)}");
        }

        private void TintWindowIcon(Color color)
        {
            var source = _window.Icon ?? GetProcessIcon();
            if (source == null)
                return;

            var tinted = ImageTinter.Tint(source, color, 64, 64);
            if (tinted == null)
                return;

            _originalIcon = _window.Icon;
            _window.Icon = tinted;
            _iconReplaced = true;
        }

        private void ApplyBadge(Color color)
        {
            var info = _window.TaskbarItemInfo;
            if (info == null)
            {
                info = new TaskbarItemInfo();
                _window.TaskbarItemInfo = info;
                _createdTaskbarInfo = true;
            }

            _originalOverlay = info.Overlay;
            info.Overlay = ImageTinter.CreateBadge(color);
            _badgeApplied = true;
        }

        private static ImageSource GetProcessIcon()
        {
            try
            {
                var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(exe))
                {
                    if (icon == null)
                        return null;
                    var bmp = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ------------------------------------------------------------------ window border (Windows 11)

        private const int DWMWA_BORDER_COLOR = 34;
        private const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFF;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint value, int size);

        private void SetBorderColor(Color color)
        {
            uint colorRef = (uint)(color.R | (color.G << 8) | (color.B << 16));
            _borderApplied = SetDwmBorder(colorRef);
        }

        private bool SetDwmBorder(uint value)
        {
            try
            {
                var hwnd = new WindowInteropHelper(_window).Handle;
                return hwnd != IntPtr.Zero && DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref value, sizeof(uint)) == 0;
            }
            catch (Exception)
            {
                return false; // pre Windows 11
            }
        }

        // ------------------------------------------------------------------ keep it applied

        private void HookReapplyEvents()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_reapplyHooked)
                return;
            _reapplyHooked = true;

            // VS may re-template or re-theme the title bar; re-assert our colors when that can happen.
            _window.StateChanged += (s, e) => { ThreadHelper.ThrowIfNotOnUIThread(); Reapply(); };
            _window.DpiChanged += (s, e) => { ThreadHelper.ThrowIfNotOnUIThread(); Reapply(); };
            Microsoft.VisualStudio.PlatformUI.VSColorTheme.ThemeChanged += e => { ThreadHelper.ThrowIfNotOnUIThread(); Reapply(); };
        }

        private void Reapply()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_settings == null)
                return;

            // Let VS finish its own layout/theme work first.
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await System.Threading.Tasks.Task.Delay(100);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (_settings != null)
                    Apply(_settings);
            }).FileAndForget("SolutionColor/Reapply");
        }

        // ------------------------------------------------------------------ helpers

        private bool IsAtTop(FrameworkElement fe)
        {
            try
            {
                return fe.TransformToAncestor(_window).Transform(new Point(0, 0)).Y < 20;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static double SafeX(Visual v, Visual ancestor)
        {
            try
            {
                return v.TransformToAncestor(ancestor).Transform(new Point(0, 0)).X;
            }
            catch (InvalidOperationException)
            {
                return double.MaxValue;
            }
        }

        private static DependencyProperty BackgroundProperty(DependencyObject d)
        {
            switch (d)
            {
                case Control _: return Control.BackgroundProperty;
                case Panel _: return Panel.BackgroundProperty;
                case Border _: return Border.BackgroundProperty;
                default: return null;
            }
        }

        private static bool SupportsBackground(DependencyObject d) => BackgroundProperty(d) != null;

        private static bool HasVisibleBackground(DependencyObject d)
        {
            var dp = BackgroundProperty(d);
            if (dp == null)
                return false;
            var brush = d.GetValue(dp) as Brush;
            if (brush == null || brush.Opacity == 0)
                return false;
            return !(brush is SolidColorBrush scb) || scb.Color.A > 0;
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root, int maxDepth = int.MaxValue)
        {
            // Breadth first so elements near the top of the tree (like the title bar) are found quickly.
            var queue = new Queue<(DependencyObject node, int depth)>();
            queue.Enqueue((root, 0));
            while (queue.Count > 0)
            {
                var (node, depth) = queue.Dequeue();
                if (node != root)
                    yield return node;
                if (depth >= maxDepth || !(node is Visual || node is System.Windows.Media.Media3D.Visual3D))
                    continue;

                int count = VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < count; i++)
                    queue.Enqueue((VisualTreeHelper.GetChild(node, i), depth + 1));
            }
        }

        private static string Describe(FrameworkElement fe) =>
            $"{fe.GetType().FullName}{(string.IsNullOrEmpty(fe.Name) ? "" : " '" + fe.Name + "'")} ({fe.ActualWidth:0}x{fe.ActualHeight:0})";

        /// <summary>Remembers a property's local value (including bindings and DynamicResource references).</summary>
        private sealed class PropertyOverride
        {
            private static readonly FieldInfo ResourceKeyField = typeof(FrameworkElement).Assembly
                .GetType("System.Windows.ResourceReferenceExpression")?
                .GetField("_resourceKey", BindingFlags.Instance | BindingFlags.NonPublic);

            private DependencyObject _target;
            private DependencyProperty _property;
            private object _localValue;
            private object _resourceKey;
            private BindingBase _binding;

            public static PropertyOverride Capture(DependencyObject target, DependencyProperty property)
            {
                var o = new PropertyOverride { _target = target, _property = property };
                var local = target.ReadLocalValue(property);
                if (local is BindingExpressionBase be)
                    o._binding = be.ParentBindingBase;
                else if (local != null && local.GetType().Name == "ResourceReferenceExpression")
                    o._resourceKey = ResourceKeyField?.GetValue(local);
                o._localValue = local;
                return o;
            }

            public void Restore()
            {
                try
                {
                    if (_binding != null)
                        BindingOperations.SetBinding(_target, _property, _binding);
                    else if (_resourceKey != null && _target is FrameworkElement fe)
                        fe.SetResourceReference(_property, _resourceKey);
                    else if (_resourceKey != null && _target is FrameworkContentElement fce)
                        fce.SetResourceReference(_property, _resourceKey);
                    else if (_localValue == DependencyProperty.UnsetValue || _localValue is Expression)
                        _target.ClearValue(_property);
                    else
                        _target.SetValue(_property, _localValue);
                }
                catch (Exception)
                {
                    _target.ClearValue(_property);
                }
            }
        }

        /// <summary>Draws a tinted copy of the adorned image on top of it.</summary>
        private sealed class ImageAdorner : Adorner
        {
            private readonly ImageSource _image;

            public ImageAdorner(UIElement adorned, ImageSource image) : base(adorned)
            {
                _image = image;
                IsHitTestVisible = false;
            }

            protected override void OnRender(DrawingContext dc)
            {
                dc.DrawImage(_image, new Rect(AdornedElement.RenderSize));
            }
        }
    }
}

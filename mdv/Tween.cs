using System;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Mdv;

/// Small framewise tweens for properties that don't have nice built-in
/// WinUI animations — `ColumnDefinition.Width` (GridLength is a struct, can't
/// be DoubleAnimation'd directly) and short fade/slide transitions on
/// elements that toggle Visibility.
public static class Tween
{
    /// Animate a Grid column's width to `to` over `duration`. Frame-based via
    /// the dispatcher queue's timer.
    public static void Column(ColumnDefinition col, double to, TimeSpan duration, Action? onComplete = null)
    {
        var from = col.Width.Value;
        if (Math.Abs(from - to) < 0.5) { col.Width = new GridLength(to); onComplete?.Invoke(); return; }

        var dispatcher = DispatcherQueue.GetForCurrentThread();
        var sw = Stopwatch.StartNew();
        DispatcherQueueTimer? timer = dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(16);
        timer.Tick += (_, _) =>
        {
            var t = Math.Min(1, sw.Elapsed.TotalMilliseconds / duration.TotalMilliseconds);
            var eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic
            col.Width = new GridLength(from + (to - from) * eased);
            if (t >= 1) { timer!.Stop(); timer = null; onComplete?.Invoke(); }
        };
        timer.Start();
    }

    /// Fade + slight slide-down on a popup-style element. Sets Visibility=Visible
    /// before animating; safe to call on already-visible elements (just retargets).
    public static void FadeSlideIn(FrameworkElement el, CompositeTransform transform, double slideFromY = -8)
    {
        el.Visibility = Visibility.Visible;
        el.Opacity = 0;
        transform.TranslateY = slideFromY;
        var sb = new Storyboard();
        sb.Children.Add(MakeAnim(el, "Opacity", 0, 1, 200));
        sb.Children.Add(MakeAnim(transform, "TranslateY", slideFromY, 0, 240));
        sb.Begin();
    }

    public static void FadeSlideOut(FrameworkElement el, CompositeTransform transform, double slideToY = -8, Action? onComplete = null)
    {
        var sb = new Storyboard();
        sb.Children.Add(MakeAnim(el, "Opacity", el.Opacity, 0, 140));
        sb.Children.Add(MakeAnim(transform, "TranslateY", transform.TranslateY, slideToY, 180));
        sb.Completed += (_, _) =>
        {
            el.Visibility = Visibility.Collapsed;
            onComplete?.Invoke();
        };
        sb.Begin();
    }

    /// Fade + scale-in for the zoom HUD-style overlay.
    public static void FadeScaleIn(FrameworkElement el, CompositeTransform transform)
    {
        el.Visibility = Visibility.Visible;
        el.Opacity = 0;
        transform.ScaleX = 0.92;
        transform.ScaleY = 0.92;
        transform.CenterX = el.ActualWidth / 2;
        transform.CenterY = el.ActualHeight / 2;
        var sb = new Storyboard();
        sb.Children.Add(MakeAnim(el, "Opacity", 0, 1, 160));
        sb.Children.Add(MakeAnim(transform, "ScaleX", 0.92, 1, 220));
        sb.Children.Add(MakeAnim(transform, "ScaleY", 0.92, 1, 220));
        sb.Begin();
    }

    public static void FadeScaleOut(FrameworkElement el, CompositeTransform transform, Action? onComplete = null)
    {
        var sb = new Storyboard();
        sb.Children.Add(MakeAnim(el, "Opacity", el.Opacity, 0, 180));
        sb.Children.Add(MakeAnim(transform, "ScaleX", transform.ScaleX, 0.96, 200));
        sb.Children.Add(MakeAnim(transform, "ScaleY", transform.ScaleY, 0.96, 200));
        sb.Completed += (_, _) =>
        {
            el.Visibility = Visibility.Collapsed;
            onComplete?.Invoke();
        };
        sb.Begin();
    }

    private static DoubleAnimation MakeAnim(DependencyObject target, string property, double from, double to, double ms)
    {
        var anim = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(ms),
            EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 4.5 },
        };
        Storyboard.SetTarget(anim, target);
        Storyboard.SetTargetProperty(anim, property);
        return anim;
    }
}

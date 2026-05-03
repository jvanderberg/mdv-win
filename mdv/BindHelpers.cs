using Microsoft.UI.Xaml;

namespace Mdv;

public static class BindHelpers
{
    public static Visibility Vis(bool b) => b ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility VisInverse(bool b) => b ? Visibility.Collapsed : Visibility.Visible;
}

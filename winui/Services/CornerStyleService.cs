using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace IdvLauncher.Services;

public static class CornerStyleService
{
    private static readonly Dictionary<DependencyObject, CornerRadius> Originals = new();

    public static bool HasOriginals => Originals.Count > 0;

    public static void Apply(DependencyObject root, bool square)
    {
        if (root is Border border)
            Set(border, square, () => border.CornerRadius, value => border.CornerRadius = value);
        else if (root is Control control)
            Set(control, square, () => control.CornerRadius, value => control.CornerRadius = value);

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
            Apply(VisualTreeHelper.GetChild(root, i), square);
    }

    private static void Set(DependencyObject element, bool square,
        Func<CornerRadius> get, Action<CornerRadius> set)
    {
        if (square)
        {
            if (Originals.ContainsKey(element)) return;
            CornerRadius original = get();
            if (original.TopLeft == 0 && original.TopRight == 0 &&
                original.BottomLeft == 0 && original.BottomRight == 0)
                return;
            Originals[element] = original;
            set(new CornerRadius(0));
        }
        else if (Originals.TryGetValue(element, out CornerRadius original))
        {
            set(original);
            Originals.Remove(element);
        }
    }
}

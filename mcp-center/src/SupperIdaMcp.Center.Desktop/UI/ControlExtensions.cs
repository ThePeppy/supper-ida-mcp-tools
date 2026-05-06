using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;

namespace SupperIdaMcp.Center.Desktop.UI;

internal static class ControlExtensions
{
    public static T WithColumn<T>(this T control, int column, int childIndex = -1) where T : Control
    {
        if (childIndex >= 0 && control is Panel panel && childIndex < panel.Children.Count)
        {
            Grid.SetColumn(panel.Children[childIndex], column);
        }
        else
        {
            Grid.SetColumn(control, column);
        }

        return control;
    }

    public static T WithRow<T>(this T control, int row) where T : Control
    {
        Grid.SetRow(control, row);
        return control;
    }

    public static T At<T>(this T control, int row, int column) where T : Control
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        return control;
    }

    public static TextBlock Ellipsis(this TextBlock textBlock)
    {
        textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
        textBlock.TextWrapping = TextWrapping.NoWrap;
        return textBlock;
    }

    public static TextBlock Wrap(this TextBlock textBlock)
    {
        textBlock.TextWrapping = TextWrapping.Wrap;
        return textBlock;
    }

    public static Border WithHover(this Border border, IBrush hoverBackground, IBrush hoverBorder)
    {
        var normalBackground = border.Background;
        var normalBorder = border.BorderBrush;
        border.Transitions =
        [
            new BrushTransition { Property = Border.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(120) },
            new BrushTransition { Property = Border.BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(120) }
        ];
        border.PointerEntered += (_, _) =>
        {
            border.Background = hoverBackground;
            border.BorderBrush = hoverBorder;
        };
        border.PointerExited += (_, _) =>
        {
            border.Background = normalBackground;
            border.BorderBrush = normalBorder;
        };
        return border;
    }
}

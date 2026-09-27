using Android.Graphics;
using Android.Graphics.Drawables;
using Color = Android.Graphics.Color;
using Paint = Android.Graphics.Paint;

namespace PurePrep;

/// <summary>
/// The decor-view background that shows through the transparent edge-to-edge bars: the top half
/// (status bar band) and bottom half (navigation bar band) each get their own colour. The content
/// view covers everything between the bands, so the split point never shows. Lets an open bottom
/// sheet dim the status band with its scrim while the navigation band continues the sheet's surface.
/// </summary>
internal sealed class BarBandsDrawable : Drawable
{
    private readonly Paint _paint = new();
    private Color _top;
    private Color _bottom;

    public void SetColors(Color top, Color bottom)
    {
        if (_top == top && _bottom == bottom)
            return;
        _top = top;
        _bottom = bottom;
        InvalidateSelf();
    }

    public override void Draw(Canvas canvas)
    {
        var b = Bounds;
        var mid = b.Top + b.Height() / 2;
        _paint.Color = _top;
        canvas.DrawRect(b.Left, b.Top, b.Right, mid, _paint);
        _paint.Color = _bottom;
        canvas.DrawRect(b.Left, mid, b.Right, b.Bottom, _paint);
    }

    public override void SetAlpha(int alpha)
    {
    }

    public override void SetColorFilter(ColorFilter? colorFilter)
    {
    }

    [Obsolete("Deprecated in API 29; still abstract on Drawable.")]
    public override int Opacity => (int)Format.Opaque;
}

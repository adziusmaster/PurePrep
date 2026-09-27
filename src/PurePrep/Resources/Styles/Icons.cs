namespace PurePrep.Resources.Styles;

/// <summary>
/// Material Symbols Rounded code points. One icon family everywhere, tinted by theme.
///
/// <c>Resources/Fonts/MaterialSymbolsRounded.ttf</c> is a static instance (FILL=0, GRAD=0,
/// opsz=24, wght=400) of the upstream variable font, subset to exactly the code points declared
/// below (~5.6 KB instead of the ~14 MB variable font). Adding a new icon means: instance the
/// upstream variable font with fonttools' <c>varLib.instancer</c>, then re-run
/// <c>pyftsubset</c> with <c>--unicodes</c> listing every code point in this file (old + new) to
/// regenerate the ttf — see the Task 13 fix report for the exact commands.
/// </summary>
public static class Icons
{
    public const string Back = "";       // chevron_left
    public const string Translate = "";  // translate
    public const string Edit = "";       // edit
    public const string More = "";       // more_vert
    public const string Delete = "";     // delete
    public const string Share = "";      // share
    public const string OpenInNew = "";  // open_in_new
    public const string Paste = "";      // content_paste
    public const string Photo = "";      // photo_camera
    public const string Text = "";       // notes
    public const string Timer = "";      // timer
    public const string Play = "";       // play_arrow
    public const string Stop = "";       // stop
    public const string Mic = "";        // mic
    public const string Settings = "";   // settings
    public const string Favourite = "";  // favorite
    public const string Search = "";     // search
    public const string Add = "";        // add
    public const string Remove = "";     // remove
    public const string Close = "";      // close
    public const string Drag = "";       // drag_indicator
    public const string Tune = "";       // tune
    public const string Reset = "";      // replay
    public const string Link = "";       // link
    public const string Check = "";      // check

    // One-off icons that don't need a shared constant elsewhere, but must still come from this same
    // font (never a raw emoji glyph).
    public const string Star = "";       // star (Smart Credits badge)
}

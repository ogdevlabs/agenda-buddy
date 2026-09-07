#if MOBILE
namespace AgendaBuddy.MobileApp.Controls;

/// <summary>
/// A person's assigned avatar, clipped to a circle.
/// </summary>
/// <remarks>
/// <para>
/// Bind <see cref="Source"/> to a model's <c>AvatarAsset</c>. Every model resolves that through
/// <c>AvatarSource</c>, so the same person shows the same mark on every surface — which was not true before:
/// Contacts honoured the server-assigned id while Messages always derived one from the email address, so any
/// account with an assigned avatar showed two different marks.
/// </para>
/// <para>
/// Abstract geometric marks rather than initials or illustrated faces (ADR-064): a random face on a named
/// individual implies a gender, age and ethnicity nobody chose.
/// </para>
/// </remarks>
public partial class Avatar : ContentView
{
    /// <summary>
    /// Edge length in device-independent units. 48 is the list-row size the messaging and contacts rows use.
    /// </summary>
    public static readonly BindableProperty SizeProperty =
        BindableProperty.Create(nameof(Size), typeof(double), typeof(Avatar), defaultValue: 48d,
            propertyChanged: OnSizeChanged);

    /// <summary>The image name, e.g. <c>avatar_07.png</c>.</summary>
    public static readonly BindableProperty SourceProperty =
        BindableProperty.Create(nameof(Source), typeof(string), typeof(Avatar), defaultValue: string.Empty);

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public string Source
    {
        get => (string)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Half the edge, so the clip is a circle at any size.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Derived rather than exposed: a radius that can be set independently of the size is a radius that can be
    /// set wrong, and every avatar in this app is round.
    /// </para>
    /// <para>
    /// ⚠️ <b>Typed <see cref="Microsoft.Maui.CornerRadius"/>, not <c>double</c>.</b> That is what
    /// <c>RoundRectangle.CornerRadius</c> takes, and a binding does NOT run the XAML type converter that makes
    /// a literal <c>CornerRadius="24"</c> work. Bound as a double it silently fails to apply, the stroke shape
    /// never resolves, and every row using this control renders blank — which looked like the whole list failing
    /// to load rather than one property failing to convert.
    /// </para>
    /// </remarks>
    public Microsoft.Maui.CornerRadius Radius => new(Size / 2d);

    public Avatar() => InitializeComponent();

    private static void OnSizeChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((Avatar)bindable).OnPropertyChanged(nameof(Radius));
}
#endif

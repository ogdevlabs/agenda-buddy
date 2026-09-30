#if MOBILE
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Routing;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.Controls;

/// <summary>
/// A showcase image, loaded through the authenticated media route and the on-disk cache.
/// </summary>
/// <remarks>
/// Showcase images are served only to a signed-in caller, so an <c>Image</c> bound to a URI cannot load them.
/// While loading, and whenever the image cannot be shown, the control keeps its background — the caller's
/// placeholder — rather than an error glyph: a missing photo is not something the viewer can act on.
/// </remarks>
public class AuthenticatedImage : ContentView
{
    public static readonly BindableProperty ProviderRefProperty =
        BindableProperty.Create(nameof(ProviderRef), typeof(string), typeof(AuthenticatedImage), null,
            propertyChanged: OnSourceChanged);

    public static readonly BindableProperty HashProperty =
        BindableProperty.Create(nameof(Hash), typeof(string), typeof(AuthenticatedImage), null,
            propertyChanged: OnSourceChanged);

    public static readonly BindableProperty VariantProperty =
        BindableProperty.Create(nameof(Variant), typeof(MediaVariant), typeof(AuthenticatedImage), MediaVariant.Thumb,
            propertyChanged: OnSourceChanged);

    public static readonly BindableProperty AspectProperty =
        BindableProperty.Create(nameof(Aspect), typeof(Aspect), typeof(AuthenticatedImage), Aspect.AspectFill,
            propertyChanged: (b, _, n) => ((AuthenticatedImage)b)._image.Aspect = (Aspect)n);

    private readonly Image _image = new() { Aspect = Aspect.AspectFill };
    private CancellationTokenSource? _loading;

    public AuthenticatedImage()
    {
        Content = _image;
        IsClippedToBounds = true;
    }

    public string? ProviderRef
    {
        get => (string?)GetValue(ProviderRefProperty);
        set => SetValue(ProviderRefProperty, value);
    }

    public string? Hash
    {
        get => (string?)GetValue(HashProperty);
        set => SetValue(HashProperty, value);
    }

    public MediaVariant Variant
    {
        get => (MediaVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public Aspect Aspect
    {
        get => (Aspect)GetValue(AspectProperty);
        set => SetValue(AspectProperty, value);
    }

    private static void OnSourceChanged(BindableObject bindable, object oldValue, object newValue) =>
        _ = ((AuthenticatedImage)bindable).LoadAsync();

    private async Task LoadAsync()
    {
        _loading?.Cancel();
        var source = AuthenticatedImageSource.For(ProviderRef, Hash, Variant);
        _image.Source = null;
        if (source is null)
            return;

        var loader = IPlatformApplication.Current?.Services.GetService<MediaImageLoader>();
        if (loader is null)
            return;

        var cts = new CancellationTokenSource();
        _loading = cts;
        try
        {
            var bytes = await loader.LoadAsync(source.ProviderRef, source.Hash, source.Variant, cts.Token);
            if (cts.IsCancellationRequested || bytes is null)
                return;

            _image.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
        }
        catch (Exception)
        {
            // The placeholder stays; see the remarks.
        }
    }
}
#endif

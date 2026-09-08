using CommunityToolkit.Mvvm.ComponentModel;
using AgendaBuddy.MobileApp.Infrastructure;

namespace AgendaBuddy.MobileApp.Models;

/// <summary>
/// One mark in the avatar picker, and whether it is the one currently chosen.
/// </summary>
/// <remarks>
/// Observable rather than a plain record because selection moves between rows while the grid is on screen, and a
/// record would need the whole collection rebuilt to redraw one ring.
/// </remarks>
public partial class AvatarChoice : ObservableObject
{
    /// <summary>The catalogue id, e.g. <c>avatar_07</c> — what gets stored.</summary>
    public required string Id { get; init; }

    /// <summary>
    /// The image name to bind, resolved through the one shared resolver.
    /// </summary>
    /// <remarks>
    /// No fallback seed, deliberately: every id here comes from the catalogue, so a null seed can never be
    /// reached. Passing the account's email would silently substitute a <i>different</i> mark for an id this build
    /// does not know, which in a picker would draw a tile that is not the tile it claims to be.
    /// </remarks>
    public string Asset => AvatarSource.For(Id, email: null);

    [ObservableProperty]
    private bool _isSelected;
}

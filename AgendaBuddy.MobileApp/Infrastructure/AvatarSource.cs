using AgendaBuddy.Library.Avatars;

namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// The one place an avatar image name is worked out, for every surface that shows a person.
/// </summary>
/// <remarks>
/// <para>
/// <b>It exists because two surfaces already disagreed.</b> The contacts list resolved
/// <c>AvatarCatalog.Resolve(assignedId, email)</c> — honouring the mark the server actually assigned — while the
/// messaging list called <c>AvatarCatalog.Deterministic(email)</c> unconditionally, ignoring any assigned id. Its
/// comment claimed the two agreed, and they do only for accounts with no assigned avatar. Every account created
/// since <c>AddProviderCommandHandler</c>/<c>AddCustomerCommandHandler</c> started assigning one is the case
/// where they differ, so the same person showed one mark in Messages and another in Contacts.
/// </para>
/// <para>
/// <b>The email is a fallback SEED, never the identity of the image.</b> An assigned id wins when there is one;
/// only a row that has none derives its mark from a SHA-256 of the address, which is what let avatars ship with
/// no migration. Deriving it from <c>string.GetHashCode()</c> would change on every app launch, since .NET
/// randomises string hash codes per process.
/// </para>
/// <para>
/// Deliberately free of MAUI types so it is covered on the <c>net10.0</c> test slice.
/// </para>
/// </remarks>
public static class AvatarSource
{
    /// <summary>
    /// The image name to bind to <c>Image.Source</c>.
    /// </summary>
    /// <param name="assignedAvatarId">
    /// The id the server assigned, when the caller has it. An unknown id is treated as absent rather than
    /// honoured, so a row written by a future build with a larger catalogue renders a real mark instead of an
    /// empty circle.
    /// </param>
    /// <param name="email">The fallback seed. Deterministic, so the same person keeps the same mark everywhere.</param>
    /// <remarks>
    /// <c>.png</c> because MAUI rasterises the committed <c>.svg</c> at build time and the asset is referenced by
    /// its raster name.
    /// </remarks>
    public static string For(string? assignedAvatarId, string? email) =>
        $"{AvatarCatalog.Resolve(assignedAvatarId, email)}.png";

    /// <summary>
    /// The mark for somebody whose assigned id this surface does not have.
    /// </summary>
    /// <remarks>
    /// Same answer as <see cref="For"/> with a null id — spelled out so a call site reads as a deliberate
    /// "we only know their address here", rather than looking like a forgotten argument.
    /// </remarks>
    public static string FromEmail(string? email) => For(null, email);
}

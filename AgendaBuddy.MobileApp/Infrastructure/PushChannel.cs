using AgendaBuddy.Library.Services;

namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// The Android notification channel every push from this app is posted on.
/// </summary>
/// <remarks>
/// <para>
/// Android 8 and later post every notification on a channel, and a message that names none lands on the one
/// the Firebase SDK auto-creates — labelled "Miscellaneous", at whatever importance the SDK picked, and
/// indistinguishable in system settings from anything else that forgot to declare a channel. Declaring one
/// gives the notifications a name a user can recognise and switch off deliberately, and lets the importance be
/// stated rather than inherited.
/// </para>
/// <para>
/// The id has to match in three places that cannot see each other: the channel <c>MainActivity</c> creates,
/// the <c>com.google.firebase.messaging.default_notification_channel_id</c> metadata in the manifest, and the
/// channel <c>FcmPushSender</c> names on every message. Two of those three are held together by taking the
/// value from <see cref="PushOptions.AndroidChannelId"/>; the manifest cannot reference C#, so
/// <c>PushConfigurationTest</c> is what holds that one. A mismatch is silent — Android falls back to the
/// auto-created channel and the declared one simply stays empty.
/// </para>
/// </remarks>
public static class PushChannel
{
    /// <summary>
    /// The sender's own constant, so the client cannot be listening on a channel the server does not use.
    /// Must also equal the manifest's <c>default_notification_channel_id</c> metadata value.
    /// </summary>
    public const string Id = PushOptions.AndroidChannelId;

    /// <summary>What the user sees in Android's notification settings for this app.</summary>
    public const string Name = "Appointments and messages";

    /// <summary>The one-line description under <see cref="Name"/> in those settings.</summary>
    public const string Description =
        "Booking requests, appointment changes and new messages.";
}

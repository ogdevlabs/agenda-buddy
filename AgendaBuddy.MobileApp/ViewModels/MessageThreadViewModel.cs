using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

public partial class MessageThreadViewModel : ObservableObject
{
    private readonly IMessagingApiService _messagingService;

    [ObservableProperty]
    private List<MessageSummary> _messages = new();

    [ObservableProperty]
    private string _threadId = string.Empty;

    [ObservableProperty]
    private string _recipientEmail = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _newMessageBody = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>Set when the thread was opened from a contact row, which knows the name.</summary>
    [ObservableProperty]
    private string _counterpartName = string.Empty;

    /// <summary>Who the thread is with — the name when we have one, the address when we do not.</summary>
    public string Title => string.IsNullOrWhiteSpace(CounterpartName) ? RecipientEmail : CounterpartName;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public MessageThreadViewModel(IMessagingApiService messagingService)
    {
        _messagingService = messagingService;
    }

    [RelayCommand]
    private async Task LoadThreadAsync()
    {
        // The real route keys on the counterpart's EMAIL, not ThreadId — MessagingRouteBuilder.Thread's own
        // remarks. ThreadId is kept only as a display/nav-state field; it was never a valid lookup key here.
        if (string.IsNullOrEmpty(RecipientEmail))
            return;

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var messages = await _messagingService.GetThreadAsync(RecipientEmail);

            // Anything NOT from the counterpart is ours. Derived from RecipientEmail rather than the session
            // because a thread only ever has two participants, so "not them" is exactly "me" — and it saves
            // this ViewModel a dependency it otherwise has no use for.
            foreach (var message in messages)
                message.IsMine = !string.Equals(
                    message.SenderEmail, RecipientEmail, StringComparison.OrdinalIgnoreCase);

            Messages = messages;
            await MarkIncomingUnreadAsync();
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.GetString("Error_LoadThread");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Marks every unread message the OTHER party sent as read — opening a thread is the point at which those
    /// messages have actually been seen. Best-effort: a failed mark-read here does not block viewing the thread.
    /// </summary>
    /// <remarks>
    /// ONE request for the whole thread (<c>POST /api/v1/messages/thread/{counterpart}/read</c>), not one per
    /// message. The per-message loop issued a sequential request — and a server-side read-then-replace — for
    /// every unread message, all of it awaited before <c>IsLoading</c> cleared: a thread with 250 unread
    /// messages left the spinner up for 250 round trips, which on a real network is the screen appearing to
    /// hang.
    /// <para>
    /// The local flags are only flipped once the server confirms, so the unread count cannot drift from what a
    /// reload reports — the same rule <c>NotificationsViewModel</c> follows.
    /// </para>
    /// </remarks>
    private async Task MarkIncomingUnreadAsync()
    {
        var unread = Messages
            .Where(m => !m.IsRead
                        && string.Equals(m.SenderEmail, RecipientEmail, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (unread.Count == 0) return;

        var marked = await _messagingService.MarkThreadReadAsync(RecipientEmail);
        if (marked is null) return;

        foreach (var message in unread)
            message.IsRead = true;
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var body = NewMessageBody;

        // Refused here as well as at the route, because the message is kept in the box for editing: sending it
        // to be rejected loses nothing but tells the sender their text was fine right up until it was not.
        if (body.Length > Routing.MessagingRouteBuilder.MaxBodyLength)
        {
            ErrorMessage = AppResources.Format(
                "Validation_MessageLength", Routing.MessagingRouteBuilder.MaxBodyLength);
            await Infrastructure.ToastNotifier.ShowAsync(ErrorMessage);
            return;
        }

        NewMessageBody = string.Empty;

        try
        {
            var result = await _messagingService.SendMessageAsync(RecipientEmail, body);

            if (!result.Succeeded)
            {
                // The service words this from what the server actually answered — a 403 is the subscription
                // rule, not a network problem, and telling somebody to check a working connection sends them
                // to fix the wrong thing.
                ErrorMessage = result.ErrorMessage ?? MessageSendResult.RejectedMessage;
                NewMessageBody = body;
                await Infrastructure.ToastNotifier.ShowAsync(ErrorMessage);
                return;
            }

            ErrorMessage = string.Empty;

            if (result.Message is not null)
            {
                result.Message.IsMine = true;
                Messages = new List<MessageSummary>(Messages) { result.Message };
            }
        }
        catch (Exception)
        {
            ErrorMessage = MessageSendResult.UnreachableMessage;
            NewMessageBody = body;
            await Infrastructure.ToastNotifier.ShowAsync(ErrorMessage);
        }
    }

    private bool CanSend() => !string.IsNullOrWhiteSpace(NewMessageBody);

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnCounterpartNameChanged(string value) => OnPropertyChanged(nameof(Title));
    partial void OnRecipientEmailChanged(string value) => OnPropertyChanged(nameof(Title));

}
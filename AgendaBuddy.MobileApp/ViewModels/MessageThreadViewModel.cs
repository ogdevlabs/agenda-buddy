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
            Messages = await _messagingService.GetThreadAsync(RecipientEmail);
            await MarkIncomingUnreadAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Could not load thread. Check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Marks every unread message the OTHER party sent as read via the real endpoint
    /// (<c>POST /api/v1/messages/{id}/read</c>) — opening a thread is the point at which those messages
    /// have actually been seen. Best-effort: a failed mark-read here does not block viewing the thread.
    /// </summary>
    private async Task MarkIncomingUnreadAsync()
    {
        var unread = Messages.Where(m => !m.IsRead && string.Equals(m.SenderEmail, RecipientEmail, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var message in unread)
        {
            try
            {
                await _messagingService.MarkReadAsync(message.Id);
                message.IsRead = true;
            }
            catch (Exception)
            {
                // Best-effort — the thread already loaded successfully; leave this one to retry next open.
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var body = NewMessageBody;
        NewMessageBody = string.Empty;

        try
        {
            var sent = await _messagingService.SendMessageAsync(RecipientEmail, body);
            if (sent is not null)
            {
                var updated = new List<MessageSummary>(Messages) { sent };
                Messages = updated;
            }
            else
            {
                ErrorMessage = "Could not send message. Check your connection and try again.";
                NewMessageBody = body;
                await Infrastructure.ToastNotifier.ShowAsync(ErrorMessage);
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Could not send message. Check your connection and try again.";
            NewMessageBody = body;
            await Infrastructure.ToastNotifier.ShowAsync(ErrorMessage);
        }
    }

    private bool CanSend() => !string.IsNullOrWhiteSpace(NewMessageBody);

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnCounterpartNameChanged(string value) => OnPropertyChanged(nameof(Title));
    partial void OnRecipientEmailChanged(string value) => OnPropertyChanged(nameof(Title));

}
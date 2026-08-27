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

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public MessageThreadViewModel(IMessagingApiService messagingService)
    {
        _messagingService = messagingService;
    }

    [RelayCommand]
    private async Task LoadThreadAsync()
    {
        if (string.IsNullOrEmpty(ThreadId))
            return;

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            Messages = await _messagingService.GetThreadAsync(ThreadId);
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
        }
        catch (Exception)
        {
            ErrorMessage = "Could not send message. Check your connection and try again.";
            NewMessageBody = body;
        }
    }

    private bool CanSend() => !string.IsNullOrWhiteSpace(NewMessageBody);

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}

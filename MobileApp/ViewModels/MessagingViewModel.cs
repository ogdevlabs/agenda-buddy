using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Models;
using MobileApp.Services;

namespace MobileApp.ViewModels;

public partial class MessagingViewModel : ObservableObject
{
    private readonly IMessagingApiService _messagingService;

    [ObservableProperty]
    private List<MessageThreadStub> _threads = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsEmpty => !IsLoading && Threads.Count == 0 && !HasError;

    public MessagingViewModel(IMessagingApiService messagingService)
    {
        _messagingService = messagingService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var results = await _messagingService.GetInboxAsync();
            Threads = results.Count > 0 ? results : GenerateSeedThreads();
        }
        catch (Exception)
        {
            Threads = GenerateSeedThreads();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    private static List<MessageThreadStub> GenerateSeedThreads()
    {
        var now = DateTime.Now;
        return
        [
            new MessageThreadStub
            {
                ThreadId = "thread-1",
                OtherPartyEmail = "alex.chen@agendabuddy.dev",
                LastMessageBody = "Hey! Can we move tomorrow's session to 10:30 AM instead?",
                LastMessageAt = now.AddMinutes(-23),
                UnreadCount = 2
            },
            new MessageThreadStub
            {
                ThreadId = "thread-2",
                OtherPartyEmail = "priya.sharma@agendabuddy.dev",
                LastMessageBody = "Thanks for the great session today! See you next week.",
                LastMessageAt = now.AddHours(-3),
                UnreadCount = 0
            },
            new MessageThreadStub
            {
                ThreadId = "thread-3",
                OtherPartyEmail = "david.thompson@agendabuddy.dev",
                LastMessageBody = "I'd like to add an extra session on Friday if you have availability.",
                LastMessageAt = now.AddHours(-8),
                UnreadCount = 1
            }
        ];
    }

    [RelayCommand]
    private void ToggleThread(MessageThreadStub thread)
    {
        thread.IsExpanded = !thread.IsExpanded;
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnThreadsChanged(List<MessageThreadStub> value) => OnPropertyChanged(nameof(IsEmpty));
}

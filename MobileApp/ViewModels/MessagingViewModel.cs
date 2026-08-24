using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Models;
using MobileApp.Services;

namespace MobileApp.ViewModels;

public partial class MessagingViewModel : ObservableObject
{
    private readonly IMessagingApiService _messagingService;
    private readonly IUserSessionService _session;

    [ObservableProperty]
    private List<MessageThreadStub> _threads = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsEmpty => !IsLoading && Threads.Count == 0 && !HasError;

    public MessagingViewModel(IMessagingApiService messagingService, IUserSessionService session)
    {
        _messagingService = messagingService;
        _session = session;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        await _session.RefreshAsync();

        try
        {
            var results = await _messagingService.GetInboxAsync();
            Threads = results;
        }
        catch (Exception)
        {
            // Real failure (network, timeout, malformed response, ambiguous write, etc.) — surface it
            // through the error banner rather than masking it with fabricated data (F-015-T08, AC8).
            ErrorMessage = "Could not load messages. Check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    [RelayCommand]
    private void ToggleThread(MessageThreadStub thread)
    {
        thread.IsExpanded = !thread.IsExpanded;

        if (thread.IsExpanded && thread.UnreadCount > 0)
            ScheduleMarkRead(thread);
    }

    private async void ScheduleMarkRead(MessageThreadStub thread)
    {
        await Task.Delay(2000);
        if (!thread.IsExpanded || thread.UnreadCount == 0)
            return;

        thread.UnreadCount = 0;
        Threads = new List<MessageThreadStub>(Threads);
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnThreadsChanged(List<MessageThreadStub> value) => OnPropertyChanged(nameof(IsEmpty));
}

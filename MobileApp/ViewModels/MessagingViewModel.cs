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
            Threads = await _messagingService.GetInboxAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Could not load messages. Check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnThreadsChanged(List<MessageThreadStub> value) => OnPropertyChanged(nameof(IsEmpty));
}

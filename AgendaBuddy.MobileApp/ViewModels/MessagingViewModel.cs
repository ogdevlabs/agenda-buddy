using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

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

    /// <summary>
    /// No page/ViewModel called <c>Shell.Current.GoToAsync("messageThread", …)</c> anywhere — the fully
    /// built <see cref="MessageThreadPage"/>/<see cref="MessageThreadViewModel"/> were unreachable dead code
    /// from a navigation standpoint. This is the "open thread" affordance that was missing.
    /// </summary>
    public event EventHandler<MessageThreadStub>? ThreadOpenRequested;

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
            // through the error banner rather than masking it with fabricated data.
            ErrorMessage = "Could not load messages. Check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    /// <summary>
    /// Opens the conversation. The only thing a row does.
    /// </summary>
    /// <remarks>
    /// A row used to expand to a copy of the preview plus an "Open conversation" button, and expanding also
    /// zeroed the unread count in memory without ever calling <c>POST /api/v1/messages/{id}/read</c> — so the
    /// badge came back on the next <c>OnAppearing</c> and nothing was ever written. Opening the thread is what
    /// marks its messages read against the real endpoint (<c>MessageThreadViewModel.LoadThreadAsync</c>), so
    /// making that the row's single action removed the fake state rather than adding a second writer for it.
    /// </remarks>
    [RelayCommand]
    private void OpenThread(MessageThreadStub thread) => ThreadOpenRequested?.Invoke(this, thread);

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnThreadsChanged(List<MessageThreadStub> value) => OnPropertyChanged(nameof(IsEmpty));
}

using MobileApp.Models;

namespace MobileApp.Services;

public interface IMessagingApiService
{
    Task<List<MessageThreadStub>> GetInboxAsync(CancellationToken ct = default);
    Task<List<MessageSummary>> GetThreadAsync(string threadId, CancellationToken ct = default);
    Task<MessageSummary?> SendMessageAsync(string recipientEmail, string body, CancellationToken ct = default);
    Task<MessageSummary?> MarkReadAsync(string id, CancellationToken ct = default);
}

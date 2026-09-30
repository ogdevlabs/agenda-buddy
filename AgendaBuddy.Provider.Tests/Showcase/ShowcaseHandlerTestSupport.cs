using AgendaBuddy.Library.Showcase;
using AgendaBuddy.Provider.Domain.Showcase;
using FluentResults;

namespace AgendaBuddy.Provider.Tests.Showcase;

internal static class ShowcaseHandlerTestSupport
{
    public const string OwnerEmail = "coach@example.com";
    public const string ViewerEmail = "ada@example.com";

    public static string Hash(char c = 'a') => new(c, 64);

    public static ProviderEntity NewProvider() => new()
    {
        Id = ObjectId.GenerateNewId(),
        FirstName = "Grace",
        LastName = "Hopper",
        Email = OwnerEmail
    };

    public static ShowcaseError Error(IResultBase result) => Assert.IsType<ShowcaseError>(Assert.Single(result.Errors));

    public static void VerifyAudited(Mock<IEventStore> eventStore, string type, string status) =>
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Type == type && ev.Status == status)), Times.Once);
}

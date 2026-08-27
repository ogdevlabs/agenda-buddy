using AgendaBuddy.Library.Entities;
using Xunit;

namespace Identity.Tests.Helpers;

/// <summary>
/// Pins the semantics of <c>GetPagedAsync</c> — F-016-T10, ADR-023's repository half.
/// </summary>
/// <remarks>
/// <para>
/// This is the only place the paging semantics can be executed inside the unit gate.
/// <c>MongoDbRepository&lt;T&gt;</c> needs a live database and the driver's fluent chain ends in an
/// extension method Moq cannot intercept (see <c>MongoDbRepositoryTest</c>), so
/// <c>InMemoryCredentialRepository</c> — a test helper, but a real implementer of the interface — is
/// where the contract becomes executable. Mongo's own <c>Skip</c>/<c>Limit</c>/
/// <c>CountDocumentsAsync</c> behaviour is exercised by <c>F-016-T15</c> through the harness.
/// </para>
/// <para>
/// The behaviours pinned here are the ones <c>api-contracts.md</c> §4 makes promises about:
/// <c>totalCount</c> counts <b>all</b> matching documents rather than the page, and a page past the
/// end is an empty page with the full count — not an error and not a 404.
/// </para>
/// </remarks>
public class InMemoryCredentialRepositoryPagingTest
{
    private static async Task<InMemoryCredentialRepository> RepositoryWith(int credentialCount)
    {
        var repository = new InMemoryCredentialRepository();
        for (var index = 0; index < credentialCount; index++)
        {
            await repository.InsertAsync(new CredentialEntity
            {
                Id = index.ToString(),
                Email = $"user{index:D3}@example.com",
                PasswordHash = "irrelevant",
                Role = "Customer",
            });
        }

        return repository;
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsOnlyTheRequestedWindow()
    {
        var repository = await RepositoryWith(10);

        var (items, _) = await repository.GetPagedAsync(skip: 4, take: 3);

        Assert.Equal(
            new[] { "user004@example.com", "user005@example.com", "user006@example.com" },
            items.Select(credential => credential.Email).ToArray());
    }

    [Fact]
    public async Task GetPagedAsync_CountsEveryDocument_NotJustThePage()
    {
        var repository = await RepositoryWith(10);

        var (items, totalCount) = await repository.GetPagedAsync(skip: 0, take: 3);

        Assert.Equal(3, items.Count());
        Assert.Equal(10L, totalCount);
    }

    [Fact]
    public async Task GetPagedAsync_PastTheEnd_IsAnEmptyPageWithTheFullCount()
    {
        // api-contracts.md section 4: "Empty array on a page past the end — 200 with [], not 404."
        var repository = await RepositoryWith(10);

        var (items, totalCount) = await repository.GetPagedAsync(skip: 500, take: 25);

        Assert.Empty(items);
        Assert.Equal(10L, totalCount);
    }

    [Fact]
    public async Task GetPagedAsync_OnAnEmptyCollection_ReturnsNothingAndCountsZero()
    {
        var repository = await RepositoryWith(0);

        var (items, totalCount) = await repository.GetPagedAsync(skip: 0, take: 25);

        Assert.Empty(items);
        Assert.Equal(0L, totalCount);
    }

    [Theory]
    [InlineData(-1, 3)]
    [InlineData(0, -1)]
    public async Task GetPagedAsync_TreatsNegativeArgumentsAsZero(int skip, int take)
    {
        // Clamping user input is the endpoint's job (ADR-023: clamp, never reject) and belongs to
        // F-016-T15. This is a different concern: MongoDB's Skip(-1) throws while LINQ's Skip(-1) is a
        // silent no-op, so without normalising, the two implementers of one interface would disagree
        // on invalid input and the divergence would only ever show up in production.
        var repository = await RepositoryWith(10);

        var (items, totalCount) = await repository.GetPagedAsync(skip, take);

        Assert.Equal(10L, totalCount);
        Assert.Equal(take <= 0 ? 0 : take, items.Count());
    }
}

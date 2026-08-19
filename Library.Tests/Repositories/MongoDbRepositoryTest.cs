using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Library.Repositories;
using Xunit;

namespace Common.Tests.Repositories;

public class MongoDbRepositoryTest
{

    [Fact]
    public void METHOD()
    {

    }

    // ── F-016-T10 · ADR-023's repository half ────────────────────────────────────────────────────
    //
    // These pin the SHAPE of the new paging primitive, not its Mongo behaviour.
    //
    // MongoDbRepository<T> cannot be unit tested: both constructors take a MongoClient or
    // IMongoDatabase (MongoDbRepository.cs:9,15), and the driver's Find(...) -> IFindFluent ->
    // ToListAsync() chain ends in an extension method, which Moq cannot intercept. Building an
    // abstraction over the driver purely to make it mockable is exactly the speculative layer the
    // yagni ladder rules out for two paginated endpoints.
    //
    // So the split, agreed at the wave-3 standup (finding E-1) and recorded so F-016-T19's
    // attestation does not overclaim:
    //   * the CONTRACT is pinned here, because F-015 is written against it and T15 consumes it;
    //   * the SEMANTICS are pinned by InMemoryCredentialRepositoryPagingTest in Identity.Tests;
    //   * Mongo's own Skip/Limit/CountDocumentsAsync behaviour gets its first real exercise through
    //     F-016-T15's paginated endpoint tests on the integration harness.
    //
    // The empty METHOD() stub above predates this feature. It is worthless as a test, but AC-19
    // forbids deleting a pre-existing one, so it stays.

    private const string PagedMethod = "GetPagedAsync";

    [Fact]
    public void IRepository_DeclaresGetPagedAsync_TakingSkipAndTake()
    {
        var method = typeof(IRepository<>).GetMethod(PagedMethod);

        Assert.NotNull(method);
        Assert.Equal(
            new[] { typeof(int), typeof(int) },
            method!.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        Assert.Equal(
            new[] { "skip", "take" },
            method.GetParameters().Select(parameter => parameter.Name).ToArray());
    }

    [Fact]
    public void GetPagedAsync_ReturnsItemsAndALongTotalCount()
    {
        // TotalCount is long because CountDocumentsAsync returns long. api-contracts.md section 4
        // records `totalCount` as long in the wire contract for the same reason, and F-015 is written
        // against that shape — so narrowing it to int here would be a breaking change to a published
        // contract, not an implementation detail.
        var entityType = typeof(IRepository<>).GetGenericArguments()[0];
        var returnType = typeof(IRepository<>).GetMethod(PagedMethod)!.ReturnType;

        Assert.Equal(typeof(Task<>), returnType.GetGenericTypeDefinition());

        var page = returnType.GetGenericArguments()[0];
        var expected = typeof(ValueTuple<,>).MakeGenericType(
            typeof(IEnumerable<>).MakeGenericType(entityType),
            typeof(long));

        Assert.Equal(expected, page);
    }

    [Fact]
    public void MongoDbRepository_ImplementsGetPagedAsync()
    {
        // Guards the failure mode that would otherwise only surface at runtime in T15: the interface
        // gains the method and one of the two implementers is forgotten.
        var implementation = typeof(MongoDbRepository<>).GetMethod(PagedMethod);

        Assert.NotNull(implementation);
    }
}

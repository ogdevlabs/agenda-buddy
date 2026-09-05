using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AgendaBuddy.Library.Repositories;
using MongoDB.Bson;
using Xunit;

namespace Common.Tests.Repositories;

public class MongoDbRepositoryTest
{

    [Fact]
    public void METHOD()
    {

    }

    // ── ADR-023's repository half ────────────────────────────────────────────────────
    //
    // These pin the SHAPE of the new paging primitive, not its Mongo behaviour.
    //
    // MongoDbRepository<T> cannot be unit tested: both constructors take a MongoClient or
    // IMongoDatabase (MongoDbRepository.cs:9,15), and the driver's Find(...) -> IFindFluent ->
    // ToListAsync() chain ends in an extension method, which Moq cannot intercept. Building an
    // abstraction over the driver purely to make it mockable is exactly the speculative layer the
    // yagni ladder rules out for two paginated endpoints.
    //
    // So the split:
    //   * the CONTRACT is pinned here, because api-contracts.md is written against it;
    //   * the SEMANTICS are pinned by InMemoryCredentialRepositoryPagingTest in AgendaBuddy.Identity.Tests;
    //   * Mongo's own Skip/Limit/CountDocumentsAsync behaviour gets its first real exercise through
    //     the paginated endpoint tests on the integration harness.
    //
    // The empty METHOD() stub above predates this feature. It is worthless as a test, but AC-19
    // forbids deleting a pre-existing one, so it stays.

    private const string PagedMethod = "GetPagedAsync";

    // GetPagedAsync is overloaded (unfiltered, and filtered by a BsonDocument), so a name-only lookup is
    // AmbiguousMatchException. Every lookup below states the parameter types it means.
    private static MethodInfo PagedBySkipTake(Type declaring) =>
        declaring.GetMethod(PagedMethod, [typeof(int), typeof(int)])!;

    private static MethodInfo PagedByFilter(Type declaring) =>
        declaring.GetMethod(PagedMethod, [typeof(BsonDocument), typeof(int), typeof(int)])!;

    [Fact]
    public void IRepository_DeclaresGetPagedAsync_TakingSkipAndTake()
    {
        var method = PagedBySkipTake(typeof(IRepository<>));

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
        // records `totalCount` as long in the wire contract for the same reason and is written
        // against that shape — so narrowing it to int here would be a breaking change to a published
        // contract, not an implementation detail.
        var entityType = typeof(IRepository<>).GetGenericArguments()[0];
        var returnType = PagedBySkipTake(typeof(IRepository<>)).ReturnType;

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
        // Guards the failure mode that would otherwise only surface at runtime: the interface
        // gains the method and one of the two implementers is forgotten.
        var implementation = PagedBySkipTake(typeof(MongoDbRepository<>));

        Assert.NotNull(implementation);
    }

    // The filtered overload exists so a caller can page a SUBSET without either short-paging (filtering
    // after the page) or loading everything (filtering a full read) -- see the interface's own remarks.
    [Fact]
    public void IRepository_DeclaresAFilteredGetPagedAsync_AndBothImplementersHaveIt()
    {
        var declared = PagedByFilter(typeof(IRepository<>));

        Assert.NotNull(declared);
        Assert.Equal(
            new[] { "filter", "skip", "take" },
            declared.GetParameters().Select(parameter => parameter.Name).ToArray());

        Assert.NotNull(PagedByFilter(typeof(MongoDbRepository<>)));
    }

    // Both overloads must agree on the page shape, or callers cannot swap one for the other.
    [Fact]
    public void BothGetPagedAsyncOverloadsReturnTheSamePageShape()
    {
        Assert.Equal(
            PagedBySkipTake(typeof(IRepository<>)).ReturnType,
            PagedByFilter(typeof(IRepository<>)).ReturnType);
    }

    // ── the partial-update primitive (ADR-032) ───────────────────────────────────────
    //
    // Same three-way split as GetPagedAsync above: contract here, semantics against the in-memory
    // implementer in AgendaBuddy.Identity.Tests/Helpers/InMemoryCredentialRepositoryUpdateTest.cs, and MongoDB's
    // own behaviour — including that it never upserts — in the integration harness's
    // CredentialUpdatePrimitiveTest. This unit test does not cover the Mongo half.

    private const string UpdateMethod = "FindOneAndUpdateAsync";

    [Fact]
    public void IRepository_DeclaresFindOneAndUpdateAsync_TakingAFilterAndAnUpdate()
    {
        var method = typeof(IRepository<>).GetMethod(UpdateMethod);

        Assert.NotNull(method);
        Assert.Equal(
            new[] { typeof(BsonDocument), typeof(BsonDocument) },
            method!.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        Assert.Equal(
            new[] { "filter", "update" },
            method.GetParameters().Select(parameter => parameter.Name).ToArray());
    }

    [Fact]
    public void FindOneAndUpdateAsync_ReturnsTheMatchedEntity_SoNullMeansNothingMatched()
    {
        var entityType = typeof(IRepository<>).GetGenericArguments()[0];
        var returnType = typeof(IRepository<>).GetMethod(UpdateMethod)!.ReturnType;

        Assert.Equal(typeof(Task<>), returnType.GetGenericTypeDefinition());
        Assert.Equal(entityType, returnType.GetGenericArguments()[0]);
    }

    [Fact]
    public void IRepository_HasExactlyTwoPartialUpdatePrimitives_OneSingleDocumentAndOneMulti()
    {
        // PRD requirement 3 forbids this growing into a query-builder abstraction. The cheapest
        // enforcement is a count: the next person who adds FindOneAndUpdateAsync(filter, update,
        // options) or a second multi-document primitive has to come here and argue for it.
        //
        // UpdateManyAsync was the first such argument, and it was accepted. Marking every unread
        // notification read is one logical operation over N documents; the alternative available here was a
        // read of N followed by N whole-document ReplaceOneAsync calls, which is precisely the
        // read-modify-write shape ADR-032 exists to remove — so refusing it would have pushed a caller into
        // the failure mode this interface is shaped to prevent. It takes the same BsonDocument filter and
        // BsonDocument update as its single-document sibling, so it adds no new abstraction style, and like
        // that sibling it never upserts.
        var partialUpdateMembers = typeof(IRepository<>)
            .GetMethods()
            .Where(method => method.Name.Contains("Update", StringComparison.Ordinal))
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "FindOneAndUpdateAsync", "UpdateAsync", "UpdateByIdentifierAsync", "UpdateManyAsync" },
            partialUpdateMembers);
    }

    /// <summary>
    /// The multi-document primitive takes the same filter/update pair as
    /// <see cref="IRepository{TEntity}.FindOneAndUpdateAsync"/> and returns a count, not documents.
    /// </summary>
    /// <remarks>
    /// The count rather than the post-images is deliberate: returning N documents would make a bulk write as
    /// expensive as the read it replaces, and no caller so far needs them. A caller that does can read.
    /// </remarks>
    [Fact]
    public void UpdateManyAsync_TakesAFilterAndAnUpdate_AndReturnsHowManyChanged()
    {
        var method = typeof(IRepository<>).GetMethod("UpdateManyAsync");

        Assert.NotNull(method);
        Assert.Equal(
            new[] { typeof(BsonDocument), typeof(BsonDocument) },
            method!.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        Assert.Equal(
            new[] { "filter", "update" },
            method.GetParameters().Select(parameter => parameter.Name).ToArray());
        Assert.Equal(typeof(Task<long>), method.ReturnType);
    }

    [Fact]
    public void MongoDbRepository_ImplementsFindOneAndUpdateAsync()
    {
        Assert.NotNull(typeof(MongoDbRepository<>).GetMethod(UpdateMethod));
    }
}

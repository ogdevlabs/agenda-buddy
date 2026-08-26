using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Library.Tools;
using MongoDB.Bson;
using Xunit;

namespace Common.Tests.Tools;

// F-019-T01. Booking/Program.cs:33-34 is the entire production JSON configuration for Booking —
// `ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new ObjectIdJsonConverter()))`
// and nothing else. These tests build JsonSerializerOptions the same way, so there is no other converter,
// naming policy, or setting production applies that this test could silently diverge from. If Booking's
// JSON configuration ever grows a second setting, update BuildProductionMatchingOptions to match, or this
// comment's claim goes stale.
//
// Design Roundtable (2026-08-26, docs/pdlc/mom/api-refactor-pilot-booking_design-roundtable_mom_2026_08_26.md):
// promoted from a throwaway spike to a permanent contract test. Booking.Domain's real DataResponse<T>
// (ADR-049) doesn't exist yet at this point in the build (T03 creates it) — TestWrapper<T> below mirrors
// its exact shape so this test doesn't need to wait on or duplicate across that project boundary.
public class ObjectIdJsonConverterTest
{
    private sealed record TestWrapper<T>(T? Data, IReadOnlyList<string> Errors);

    private sealed record TestResponse(ObjectId Identifier, string Status);

    private static JsonSerializerOptions BuildProductionMatchingOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ObjectIdJsonConverter());
        return options;
    }

    [Fact]
    public void Serialize_NestedObjectIdInsideGenericWrapper_WritesAsPlainHexString()
    {
        var options = BuildProductionMatchingOptions();
        var id = ObjectId.GenerateNewId();
        var wrapper = new TestWrapper<TestResponse>(new TestResponse(id, "Booked"), []);

        var json = JsonSerializer.Serialize(wrapper, options);

        Assert.Contains($"\"Identifier\":\"{id}\"", json);
        Assert.DoesNotContain("timestamp", json);
    }

    [Fact]
    public void RoundTrip_NestedObjectIdInsideGenericWrapper_DeserializesBackToTheSameObjectId()
    {
        var options = BuildProductionMatchingOptions();
        var id = ObjectId.GenerateNewId();
        var wrapper = new TestWrapper<TestResponse>(new TestResponse(id, "Booked"), []);

        var json = JsonSerializer.Serialize(wrapper, options);
        var roundTripped = JsonSerializer.Deserialize<TestWrapper<TestResponse>>(json, options);

        Assert.Equal(id, roundTripped!.Data!.Identifier);
    }

    [Fact]
    public void Serialize_FailCaseWithNullData_DoesNotThrow()
    {
        var options = BuildProductionMatchingOptions();
        var wrapper = new TestWrapper<TestResponse>(null, ["Appointment not found"]);

        var json = JsonSerializer.Serialize(wrapper, options);
        var roundTripped = JsonSerializer.Deserialize<TestWrapper<TestResponse>>(json, options);

        Assert.Null(roundTripped!.Data);
        Assert.Equal(["Appointment not found"], roundTripped.Errors);
    }

    [Fact]
    public void RoundTrip_CollectionOfObjectIdBackedItemsInsideWrapper_ConvertsEveryItem()
    {
        var options = BuildProductionMatchingOptions();
        var first = new TestResponse(ObjectId.GenerateNewId(), "Booked");
        var second = new TestResponse(ObjectId.GenerateNewId(), "Completed");
        var wrapper = new TestWrapper<IReadOnlyList<TestResponse>>([first, second], []);

        var json = JsonSerializer.Serialize(wrapper, options);
        var roundTripped = JsonSerializer.Deserialize<TestWrapper<List<TestResponse>>>(json, options);

        Assert.Equal([first.Identifier, second.Identifier], roundTripped!.Data!.ConvertAll(r => r.Identifier));
    }

    [Fact]
    public void RoundTrip_ConverterOrderedAfterAnotherConverter_StillFiresForObjectId()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new ObjectIdJsonConverter());
        var id = ObjectId.GenerateNewId();
        var wrapper = new TestWrapper<TestResponse>(new TestResponse(id, "Booked"), []);

        var json = JsonSerializer.Serialize(wrapper, options);
        var roundTripped = JsonSerializer.Deserialize<TestWrapper<TestResponse>>(json, options);

        Assert.Equal(id, roundTripped!.Data!.Identifier);
    }
}

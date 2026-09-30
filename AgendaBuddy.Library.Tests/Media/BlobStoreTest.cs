using AgendaBuddy.Library.Media;
using MongoDB.Bson;
using Xunit;

namespace AgendaBuddy.Library.Tests.Media;

public class BlobStoreTest
{
    [Fact]
    public async Task InMemory_PutThenOpen_ReturnsACopyOfTheBytes()
    {
        var store = new InMemoryBlobStore();
        var content = new byte[] { 1, 2, 3 };

        await store.PutAsync("p/h/full", content, "image/jpeg");
        content[0] = 9;

        await using var stream = await store.OpenReadAsync("p/h/full");
        Assert.NotNull(stream);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        Assert.Equal(new byte[] { 1, 2, 3 }, copy.ToArray());
    }

    [Fact]
    public async Task InMemory_OpenOfAnUnknownKey_IsNull()
    {
        var store = new InMemoryBlobStore();

        Assert.Null(await store.OpenReadAsync("missing"));
        Assert.False(await store.ExistsAsync("missing"));
    }

    [Fact]
    public async Task InMemory_DeletePrefix_RemovesOnlyThatPrefix()
    {
        var store = new InMemoryBlobStore();
        await store.PutAsync("a/1/full", [1], "image/jpeg");
        await store.PutAsync("a/1/thumb", [1], "image/jpeg");
        await store.PutAsync("a/2/full", [1], "image/jpeg");
        await store.PutAsync("b/1/full", [1], "image/jpeg");

        await store.DeletePrefixAsync("a/1/");

        Assert.False(await store.ExistsAsync("a/1/full"));
        Assert.False(await store.ExistsAsync("a/1/thumb"));
        Assert.True(await store.ExistsAsync("a/2/full"));
        Assert.True(await store.ExistsAsync("b/1/full"));
    }

    [Fact]
    public async Task InMemory_List_ReturnsSortedKeysUnderThePrefix()
    {
        var store = new InMemoryBlobStore();
        await store.PutAsync("a/2/full", [1], "image/jpeg");
        await store.PutAsync("a/1/full", [1], "image/jpeg");
        await store.PutAsync("b/1/full", [1], "image/jpeg");

        Assert.Equal(["a/1/full", "a/2/full"], await store.ListAsync("a/"));
        Assert.Equal(["a", "b"], await store.ListTopLevelPrefixesAsync());
    }

    [Fact]
    public async Task Unavailable_EveryOperation_ThrowsMediaStorageUnavailable()
    {
        var store = new UnavailableBlobStore();

        await Assert.ThrowsAsync<MediaStorageUnavailableException>(() => store.PutAsync("k", [1], "image/jpeg"));
        await Assert.ThrowsAsync<MediaStorageUnavailableException>(() => store.OpenReadAsync("k"));
        await Assert.ThrowsAsync<MediaStorageUnavailableException>(() => store.ExistsAsync("k"));
        await Assert.ThrowsAsync<MediaStorageUnavailableException>(() => store.DeletePrefixAsync("k"));
        await Assert.ThrowsAsync<MediaStorageUnavailableException>(() => store.ListAsync("k"));
        await Assert.ThrowsAsync<MediaStorageUnavailableException>(() => store.ListTopLevelPrefixesAsync());
    }

    [Fact]
    public void MediaKeys_AreProviderHashVariant()
    {
        var providerId = ObjectId.GenerateNewId();

        Assert.Equal($"{providerId}/abc/full", MediaKeys.For(providerId, "abc", MediaKeys.Full));
        Assert.Equal($"{providerId}/", MediaKeys.Prefix(providerId));
    }

    [Theory]
    [InlineData("full", true)]
    [InlineData("thumb", true)]
    [InlineData("Full", false)]
    [InlineData("original", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void MediaKeys_IsVariant_AcceptsOnlyFullAndThumb(string? variant, bool expected) =>
        Assert.Equal(expected, MediaKeys.IsVariant(variant));
}

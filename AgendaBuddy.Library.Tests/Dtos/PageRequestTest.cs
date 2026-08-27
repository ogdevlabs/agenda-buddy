using AgendaBuddy.Library.Dtos;
using Xunit;

namespace Common.Tests.Dtos;

/// <summary>
/// Pins <see cref="PageRequest"/> — F-016-T15, AC-15, the clamping half of ADR-023.
/// </summary>
/// <remarks>
/// <para>
/// <b>The cap is a security control, not ergonomics.</b> An uncapped <c>pageSize</c> restores exactly the
/// full-dataset dump this feature exists to remove, so it is enforced server-side and lives in a pure
/// function with its own tests rather than inline in two endpoints.
/// </para>
/// <para>
/// <b>Clamped, never rejected</b> (ADR-023). Returning 400 would tell an attacker the exact boundary and
/// leave an honest client no way to discover the cap. Clamping plus echoing the <em>effective</em> value
/// lets a well-behaved client detect it and paginate correctly.
/// </para>
/// </remarks>
public class PageRequestTest
{
    [Fact]
    public void MaxPageSize_IsOneHundred()
    {
        // Pinned as a number because F-015 is written against it (api-contracts.md section 4).
        Assert.Equal(100, PageRequest.MaxPageSize);
    }

    [Fact]
    public void Defaults_ArePageOneAndTwentyFive()
    {
        var page = PageRequest.Clamp(null, null);

        Assert.Equal(1, page.Page);
        Assert.Equal(25, page.PageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void APageBelowOne_ClampsToOne(int requested)
    {
        Assert.Equal(1, PageRequest.Clamp(requested, null).Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void APageSizeBelowOne_ClampsToTheDefault(int requested)
    {
        Assert.Equal(25, PageRequest.Clamp(null, requested).PageSize);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(100_000)]
    [InlineData(int.MaxValue)]
    public void APageSizeAboveTheMaximum_ClampsToTheMaximum(int requested)
    {
        // The security-relevant case. int.MaxValue is included because that is what an attacker sends.
        Assert.Equal(PageRequest.MaxPageSize, PageRequest.Clamp(null, requested).PageSize);
    }

    [Fact]
    public void AValidRequest_IsPassedThroughUnchanged()
    {
        var page = PageRequest.Clamp(3, 50);

        Assert.Equal(3, page.Page);
        Assert.Equal(50, page.PageSize);
    }

    [Theory]
    [InlineData(1, 25, 0)]
    [InlineData(2, 25, 25)]
    [InlineData(4, 10, 30)]
    public void Skip_IsDerivedFromTheClampedValues(int page, int pageSize, int expectedSkip)
    {
        Assert.Equal(expectedSkip, PageRequest.Clamp(page, pageSize).Skip);
    }

    [Fact]
    public void Skip_CannotOverflowIntoANegativeValue()
    {
        // (page - 1) * pageSize with a huge page would overflow to a negative skip, and a negative skip is
        // what MongoDB's Skip() rejects. Clamped page keeps this bounded, but the arithmetic is checked
        // because an overflow here would be a 500 on an attacker-controlled input.
        var page = PageRequest.Clamp(int.MaxValue, PageRequest.MaxPageSize);

        Assert.True(page.Skip >= 0, $"skip overflowed to {page.Skip}");
    }
}

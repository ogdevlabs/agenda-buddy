namespace AgendaBuddy.IntegrationTests.OpenApi;

/// <summary>
/// The seven services <see cref="OpenApiSpecGenerator"/> covers, by name — the single source of truth
/// for "all 7" that both the coverage test and a real generation run iterate over.
/// </summary>
/// <remarks>
/// A <c>Dictionary&lt;string, Func&lt;string, string&gt;&gt;</c> rather than reflection over
/// <c>EntryPoints.All</c>: <see cref="OpenApiSpecGenerator.Generate{TEntryPoint}"/> is generic, and its
/// type argument has to be a compile-time anchor type (see <c>GlobalUsings.cs</c>'s
/// <c>*Anchor</c> aliases) — an explicit list is simpler than <c>MakeGenericMethod</c> reflection for
/// seven known, fixed entries.
/// </remarks>
public static class OpenApiSpecCatalog
{
    public static readonly IReadOnlyDictionary<string, Func<string, string>> Generators =
        new Dictionary<string, Func<string, string>>
        {
            ["Booking"] = pem => OpenApiSpecGenerator.Generate<BookingAnchor>(pem),
            ["Calendar"] = pem => OpenApiSpecGenerator.Generate<CalendarAnchor>(pem),
            ["Customer"] = pem => OpenApiSpecGenerator.Generate<CustomerAnchor>(pem),
            ["Provider"] = pem => OpenApiSpecGenerator.Generate<ProviderAnchor>(pem),
            ["Services"] = pem => OpenApiSpecGenerator.Generate<ServicesAnchor>(pem),
            ["Profession"] = pem => OpenApiSpecGenerator.Generate<ProfessionAnchor>(pem),
            ["Identity"] = pem => OpenApiSpecGenerator.Generate<IdentityAnchor>(pem),
        };
}

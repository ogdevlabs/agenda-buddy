using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// What a person reads for each showcase and media error code. A raw code on screen tells nobody what to do, so
/// every code in <see cref="ShowcaseErrorCodes.All"/> has copy, and an unrecognised one falls back to the generic
/// line rather than to nothing.
/// </summary>
public static class ShowcaseErrorCopy
{
    public static string For(string? code) => AppResources.GetString(KeyFor(code));

    /// <summary>
    /// The copy for a failed result. A destination the Gateway could not reach is named ("Providers is
    /// unavailable") rather than described as a storage or network failure.
    /// </summary>
    public static string Describe<T>(ShowcaseResult<T> result) =>
        result.FailedService is { Length: > 0 } service
            ? GatewayErrorMapper.Describe(service)
            : For(result.ErrorCode);

    public static string KeyFor(string? code) => code switch
    {
        ShowcaseErrorCodes.UnsupportedFormat => "ShowcaseError_UnsupportedFormat",
        ShowcaseErrorCodes.TooLarge => "ShowcaseError_TooLarge",
        ShowcaseErrorCodes.TooManyPixels => "ShowcaseError_TooManyPixels",
        ShowcaseErrorCodes.DimensionExceeded => "ShowcaseError_DimensionExceeded",
        ShowcaseErrorCodes.Undecodable => "ShowcaseError_Undecodable",
        ShowcaseErrorCodes.UnknownMedia => "ShowcaseError_UnknownMedia",
        ShowcaseErrorCodes.StorageUnavailable => "ShowcaseError_StorageUnavailable",
        ShowcaseErrorCodes.PortfolioFull => "ShowcaseError_PortfolioFull",
        ShowcaseErrorCodes.PortfolioChanged => "ShowcaseError_PortfolioChanged",
        ShowcaseErrorCodes.ShowcaseNotFound => "ShowcaseError_NotFound",
        ShowcaseErrorCodes.RateLimited => "ShowcaseError_RateLimited",
        ShowcaseErrorCodes.Invalid => "ShowcaseError_Invalid",
        ShowcaseErrorCodes.Network => "ShowcaseError_Network",
        _ => "ShowcaseError_Unknown"
    };
}

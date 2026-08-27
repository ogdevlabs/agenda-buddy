namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// Pairs an HTTP verb with a relative path, as decided by a route-builder class (F-015-T06).
/// The corresponding *ApiService issues the request via the client method matching
/// <see cref="Method"/>; this type exists purely so that decision is directly assertable
/// in a unit test running under the net10.0 fallback TFM, with no Maui or DI dependency.
/// </summary>
public readonly record struct RouteSpec(HttpMethod Method, string Path);

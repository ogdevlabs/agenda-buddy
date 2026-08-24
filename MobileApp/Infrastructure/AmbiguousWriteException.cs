namespace MobileApp.Infrastructure;

/// <summary>
/// Thrown when a non-idempotent write (<c>POST</c>, or a <c>PUT</c> not provably idempotent —
/// e.g. an appointment status transition) fails ambiguously at the gateway hop: a request timeout,
/// or a 502/504 from the gateway. In either case the backend may already have processed the write.
///
/// AC10 (F-015-T09): the client must never silently auto-retry in this situation. Callers
/// (ViewModels) should catch this distinctly from a plain <see cref="HttpRequestException"/> or
/// <see cref="TaskCanceledException"/> and render an "unknown result — check before retrying"
/// state, rather than a generic failure message that invites the user to just try again.
/// </summary>
public class AmbiguousWriteException : Exception
{
    public AmbiguousWriteException(string message)
        : base(message)
    {
    }

    public AmbiguousWriteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using AgendaBuddy.Library.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Moq;
using Xunit;

namespace Common.Tests.Security;

/// <summary>
/// Unit-level pins for <see cref="AgendaBuddyExceptionHandler"/> — F-016-T08, ADR-022 / ARCHITECTURE AD-1.
/// </summary>
/// <remarks>
/// <para>
/// The end-to-end behaviour (AC-13, AC-14 and AC-23's <c>Production</c> requirement) is asserted over
/// real HTTP by <c>CentralForbiddenTest</c> in the integration harness. These tests pin the two
/// properties that are cheaper and clearer to state in isolation: that the handler <b>declines</b>
/// everything except <see cref="ForbiddenException"/>, and that it never hands the exception to the
/// <see cref="IProblemDetailsService"/>.
/// </para>
/// <para>
/// The second is threat <b>T-004</b>'s whole safety margin. Today an unhandled
/// <c>ForbiddenException</c> in <c>Production</c> produces a <em>bare, empty-bodied</em> 500 —
/// accidentally the most conservative response possible. T08 starts emitting a body where none
/// existed, so "no exception type, no message, no stack frame" is not belt-and-braces.
/// </para>
/// </remarks>
public class AgendaBuddyExceptionHandlerTest
{
    private static (AgendaBuddyExceptionHandler Handler, Mock<IProblemDetailsService> Writer) Subject()
    {
        var writer = new Mock<IProblemDetailsService>();
        writer.Setup(w => w.TryWriteAsync(It.IsAny<ProblemDetailsContext>())).ReturnsAsync(true);
        return (new AgendaBuddyExceptionHandler(writer.Object), writer);
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(FormatException))]
    public async Task TryHandleAsync_DeclinesEveryExceptionExceptForbidden(Type exceptionType)
    {
        // Returning false is what lets this handler coexist with the Development-only
        // UseExceptionHandler lambda: the exception is rethrown and propagates outward to it, so
        // nothing that works today changes. FormatException is included deliberately -- api-contracts
        // section 3.3 names it the most likely live 500, and ADR-022 leaves it unmapped ON PURPOSE.
        // If someone later "helpfully" maps it here, this test fails and the contract change is noticed.
        var (handler, writer) = Subject();
        var context = new DefaultHttpContext();
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        writer.Verify(w => w.TryWriteAsync(It.IsAny<ProblemDetailsContext>()), Times.Never);
    }

    [Fact]
    public async Task TryHandleAsync_ForForbiddenException_Sets403AndWritesProblemDetails()
    {
        var (handler, writer) = Subject();
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(context, new ForbiddenException(), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        writer.Verify(w => w.TryWriteAsync(It.IsAny<ProblemDetailsContext>()), Times.Once);
    }

    [Fact]
    public async Task T004_NeverGivesTheProblemDetailsWriterAnythingDerivedFromTheException()
    {
        // The written body must carry status, title and requestId ONLY. requestId is added downstream by
        // each service's CustomizeProblemDetails extension (Activity.Current?.Id), which is why the
        // handler goes through IProblemDetailsService rather than writing JSON itself.
        var (handler, writer) = Subject();
        ProblemDetailsContext? captured = null;
        writer.Setup(w => w.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(c => captured = c)
            .ReturnsAsync(true);

        await handler.TryHandleAsync(
            new DefaultHttpContext(),
            new ForbiddenException("a message that must not be echoed"),
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(StatusCodes.Status403Forbidden, captured!.ProblemDetails.Status);
        Assert.Equal("Forbidden", captured.ProblemDetails.Title);

        // No exception message, no exception type, no stack frame -- by omission, not by sanitising.
        Assert.Null(captured.ProblemDetails.Detail);
        Assert.Null(captured.Exception);
        Assert.Empty(captured.ProblemDetails.Extensions);
    }
}

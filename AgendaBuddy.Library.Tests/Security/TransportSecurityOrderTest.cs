using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Common.Tests.Security;

/// <summary>
/// Pins F-021 AC-12: every service registers transport security <b>before</b> authentication.
/// </summary>
/// <remarks>
/// <para>
/// All seven services used to call <c>UseHttpsRedirection</c> <i>after</i> <c>UseAuthentication</c> — so
/// the bearer token was parsed and validated out of a plaintext request, and only then was the client
/// told to come back over TLS. AgendaBuddy.Identity, which receives passwords, additionally wrapped its redirect in
/// <c>if (!IsDevelopment())</c>, a condition that means nothing here because the AppHost runs every
/// service as <b>Production</b>.
/// </para>
/// <para>
/// <b>Why a source-text assertion.</b> Middleware order is not observable from a built application:
/// <c>IApplicationBuilder</c> exposes no ordered list of registered components, and the pipeline is a
/// composed delegate by the time anything can look at it. The alternatives were hosting all seven
/// services (a container each, for a question about two lines of text) or asserting nothing. F-016
/// established the precedent for tree-level checks living in <c>Library.Tests</c>, where the existing
/// <c>api</c> CI job runs them on every pull request rather than only when someone remembers the
/// Docker-dependent suite.
/// </para>
/// <para>
/// ⚠️ <b>This test reads source, so it is sensitive to how the calls are written</b> — a
/// <c>using</c>-alias or a wrapper helper would evade it. That is the accepted cost of asserting order at
/// all; the failure mode is a false pass, never a false failure, and the second assertion below closes
/// the most likely evasion by banning direct <c>UseHttpsRedirection</c> calls outright.
/// </para>
/// <para>
/// <b>F-015-T01: Gateway (renamed <c>AgendaBuddy.Gateway</c> by F-020-T04) is an eighth process, added
/// to <see cref="AllServices"/> below, but it is not in the seven-item <c>[Theory]</c> data below.</b>
/// It has no authentication middleware of its own (ARCHITECTURE.md §2's "Auth passthrough" decision —
/// it forwards the <c>Authorization</c> header unvalidated once F-015-T03 adds YARP), so
/// <c>TransportSecurity_IsRegisteredBeforeAuthentication</c>'s assertion relative to
/// <c>app.UseAuthentication()</c> does not apply to it. <see cref="GatewayCallsTransportSecurity"/> below
/// covers the part that does apply: the call must still be present.
/// </para>
/// </remarks>
public class TransportSecurityOrderTest
{
    private static readonly string[] AllServices =
        ["AgendaBuddy.Gateway", "AgendaBuddy.Identity", "AgendaBuddy.Booking.Api", "AgendaBuddy.Calendar.Api", "Customer", "AgendaBuddy.Profession.Api", "Provider", "AgendaBuddy.Services.Api"];

    private const string TransportSecurityCall = "UseAgendaBuddyTransportSecurity(";
    private const string AuthenticationCall = "app.UseAuthentication()";
    private const string RedirectionCall = "app.UseHttpsRedirection()";

    [Fact]
    public void EveryServiceProgramFile_IsAccountedFor()
    {
        // Guards the way this test could quietly stop covering things: a new service appears, nobody
        // adds it here, and the suite still reports green for "all eight".
        var actual = Directory
            .GetDirectories(RepositoryRoot())
            .Select(Path.GetFileName)
            .Where(name => name is not null
                           && File.Exists(Path.Combine(RepositoryRoot(), name, "Program.cs"))
                           && !string.Equals(name, "AgendaBuddy.AppHost", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(AllServices.OrderBy(name => name, StringComparer.Ordinal).ToArray(), actual);
    }

    [Fact]
    public void GatewayCallsTransportSecurity()
    {
        // Gateway has no `app.UseAuthentication()` to be ordered against (no auth middleware of its own
        // — ARCHITECTURE.md §2), so it gets its own assertion rather than joining the theory below.
        var source = ProgramSource("AgendaBuddy.Gateway");

        Assert.True(
            source.Contains(TransportSecurityCall, StringComparison.Ordinal),
            $"AgendaBuddy.Gateway/Program.cs does not call {TransportSecurityCall} at all, so it registers "
            + "no HSTS and no HTTPS redirect (F-021 AC-12 / F-015-T01).");

        Assert.DoesNotContain(RedirectionCall, source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("AgendaBuddy.Identity")]
    [InlineData("AgendaBuddy.Booking.Api")]
    [InlineData("AgendaBuddy.Calendar.Api")]
    [InlineData("Customer")]
    [InlineData("AgendaBuddy.Profession.Api")]
    [InlineData("Provider")]
    [InlineData("AgendaBuddy.Services.Api")]
    public void TransportSecurity_IsRegisteredBeforeAuthentication(string service)
    {
        var source = ProgramSource(service);

        var transportSecurity = source.IndexOf(TransportSecurityCall, StringComparison.Ordinal);
        var authentication = source.IndexOf(AuthenticationCall, StringComparison.Ordinal);

        Assert.True(
            transportSecurity >= 0,
            $"{service}/Program.cs does not call {TransportSecurityCall} at all, so it registers no HSTS "
            + "and no HTTPS redirect (F-021 AC-12). ServiceDefaults owns the policy, but placing the "
            + "middleware is each service's own line — AddServiceDefaults runs on the builder, before a "
            + "pipeline exists, so it cannot position anything.");

        Assert.True(
            authentication >= 0,
            $"{service}/Program.cs does not call {AuthenticationCall}, which this assertion is relative "
            + "to. If the pipeline was restructured, update this test deliberately rather than deleting "
            + "it.");

        Assert.True(
            transportSecurity < authentication,
            $"{service}/Program.cs registers transport security AFTER authentication (character "
            + $"{transportSecurity} vs {authentication}). The bearer token is then parsed out of a "
            + "plaintext request before the redirect is issued — the exact defect F-021 requirement 13 "
            + "exists to fix.");
    }

    [Theory]
    [InlineData("AgendaBuddy.Identity")]
    [InlineData("AgendaBuddy.Booking.Api")]
    [InlineData("AgendaBuddy.Calendar.Api")]
    [InlineData("Customer")]
    [InlineData("AgendaBuddy.Profession.Api")]
    [InlineData("Provider")]
    [InlineData("AgendaBuddy.Services.Api")]
    public void NoService_CallsUseHttpsRedirectionDirectly(string service)
    {
        // One implementation, seven call sites. A service that adds its own redirect back gets a second,
        // unordered one — which is how the original defect would return: not by someone moving a line,
        // but by someone adding one.
        Assert.DoesNotContain(RedirectionCall, ProgramSource(service), StringComparison.Ordinal);
    }

    private static string ProgramSource(string service) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), service, "Program.cs"));

    /// <summary>
    /// The repository root, found by walking up for <c>.git</c>. Fails closed rather than reporting a
    /// vacuous pass, exactly as <see cref="KeyMaterialHygieneTest"/> does.
    /// </summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath)) return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"No `.git` found above {AppContext.BaseDirectory}. This test reads the seven Program.cs "
            + "files from the working tree and fails closed rather than passing vacuously (F-021 AC-12).");
    }
}

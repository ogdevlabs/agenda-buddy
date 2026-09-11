using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

public class EmailVerificationFlowTest
{
    private static string RepoRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([RepoRoot(), .. parts]));

    [Fact]
    public void RegistrationShowsCheckEmailToastBeforeOpeningVerification()
    {
        var source = Read("AgendaBuddy.MobileApp", "Views", "RegisterPage.xaml.cs");
        var toast = source.IndexOf("ToastNotifier.ShowAsync", StringComparison.Ordinal);
        var navigation = source.IndexOf("//emailVerification", StringComparison.Ordinal);

        Assert.True(toast >= 0 && toast < navigation,
            "Registration must show the check-email toast before navigating to verification.");
    }

    [Fact]
    public void VerificationScreenAcceptsSixDigitCodeAndRedirectsSuccessToLogin()
    {
        var xaml = Read("AgendaBuddy.MobileApp", "Views", "EmailVerificationPage.xaml");
        var codeBehind = Read("AgendaBuddy.MobileApp", "Views", "EmailVerificationPage.xaml.cs");

        Assert.Contains("MaxLength=\"6\"", xaml);
        Assert.Contains("Command=\"{Binding ConfirmCodeCommand}\"", xaml);
        Assert.Contains("VerificationSucceeded += OnVerificationSucceeded", codeBehind);
        Assert.Contains("GoToAsync(\"//login\")", codeBehind);
    }
}
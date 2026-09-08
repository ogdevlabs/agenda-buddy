namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// One clause of a legal document: a heading and its body.
/// </summary>
public record LegalClause(string Heading, string Body);

/// <summary>
/// A legal document — its title, its effective date, and its clauses.
/// </summary>
public record LegalDocument(string Title, string EffectiveDate, string Summary, IReadOnlyList<LegalClause> Clauses)
{
    /// <summary>The document as one plain-text block, for a share sheet or a copy action.</summary>
    public string ToPlainText() =>
        string.Join(
            Environment.NewLine + Environment.NewLine,
            new[] { Title, $"Effective {EffectiveDate}", Summary }
                .Concat(Clauses.Select(clause => $"{clause.Heading}{Environment.NewLine}{clause.Body}")));
}

/// <summary>
/// The Terms and Conditions and the Privacy Policy, as text this build ships.
/// </summary>
/// <remarks>
/// <para>
/// <b>In the app rather than only on a website, and that is the point.</b> App Review requires the text to be
/// reachable, and a link is only reachable when there is a network and the site exists. Shipping the text means
/// the consent checkboxes on the profile editor can always be read before they are ticked — a checkbox next to an
/// unreachable link is a consent nobody gave.
/// </para>
/// <para>
/// ⚠️ <b>These are minimal, industry-standard clauses, not legal advice, and they are not a substitute for a
/// lawyer's review before public release.</b> They describe what this product actually does — schedules
/// appointments between a provider and a customer, stores names, addresses, phone numbers and appointment
/// records, sends email and push — and deliberately claim nothing it does not do. The App Store listing
/// separately needs a hosted privacy URL (agenda-buddy-1hk.14); <see cref="TermsUrl"/> and
/// <see cref="PrivacyUrl"/> are where that goes once the site exists.
/// </para>
/// <para>
/// Free of MAUI types, so both documents' structure and completeness are covered on the <c>net10.0</c> test slice
/// — the same reason <c>NotificationVisuals</c> holds plain hex strings.
/// </para>
/// </remarks>
public static class LegalDocuments
{
    /// <summary>
    /// The date both documents took effect. One constant, because they were written together and a screen showing
    /// two different dates invites the question of which one is current.
    /// </summary>
    public const string EffectiveDate = "8 September 2026";

    /// <summary>
    /// Where the Terms will be published. ⚠️ <b>The site does not exist yet</b> — the in-app document is the
    /// canonical copy until it does, which is why nothing links out to this today.
    /// </summary>
    public const string TermsUrl = "https://agendame.app/terms";

    /// <summary>
    /// Where the Privacy Policy will be published. Same caveat as <see cref="TermsUrl"/>, and this is the URL the
    /// App Store listing will need.
    /// </summary>
    public const string PrivacyUrl = "https://agendame.app/privacy";

    /// <summary>Who to contact about either document.</summary>
    public const string ContactEmail = "privacy@agendame.app";

    public static readonly LegalDocument Terms = new(
        Title: "Terms and Conditions",
        EffectiveDate: EffectiveDate,
        Summary:
        $"These terms govern your use of {AppBrand.Name}, a scheduling app that connects independent service "
        + "providers with their customers. By creating an account or using the app you agree to them. If you do "
        + "not agree, do not use the app.",
        Clauses:
        [
            new("1. Your account",
                "You must give accurate information when you register and keep your password confidential. You "
                + "are responsible for everything done through your account. One person, one account. You must "
                + "be old enough to enter a contract where you live."),

            new("2. What the service does",
                $"{AppBrand.Name} lets providers publish their availability and services, and lets customers "
                + "request, book, reschedule and cancel appointments. We provide the scheduling tool. We are not "
                + "a party to any appointment, we do not employ providers, and we do not supervise, endorse or "
                + "guarantee any service delivered through the app."),

            new("3. Appointments, cancellation and payment",
                "An appointment is an agreement between the provider and the customer. Cancellation is subject "
                + "to the app's cancellation window and to whatever the provider tells you separately. Where "
                + "payment is taken it is processed by a third-party payment provider under their own terms; we "
                + "do not store your card details."),

            new("4. Acceptable use",
                "Do not use the app to harass anyone, to impersonate anyone, to send unsolicited marketing, to "
                + "collect other people's information, to break the law, or to interfere with, probe or overload "
                + "the service. Do not attempt to access data that is not yours."),

            new("5. Your content",
                "You keep ownership of what you put into the app — your profile, your services, your messages and "
                + "your notes. You grant us the permission we need to store it and show it to the people you "
                + "direct it to, and nothing more."),

            new("6. Suspension and deletion",
                "You can delete your account at any time from the Profile screen. We may suspend or close an "
                + "account that breaks these terms. Deleting your account removes your profile and your address "
                + "from the records of people you dealt with; it does not delete their own record of appointments "
                + "that took place."),

            new("7. Availability and disclaimer",
                "The app is provided as is. We do not promise it will be uninterrupted or error-free, and we do "
                + "not guarantee that a provider will honour a booking or that a customer will attend one. Back "
                + "up anything you cannot afford to lose."),

            new("8. Limitation of liability",
                "To the extent the law allows, we are not liable for indirect or consequential loss, for lost "
                + "profits, or for anything arising out of a service a provider delivered or failed to deliver. "
                + "Nothing here limits liability that cannot lawfully be limited."),

            new("9. Changes to these terms",
                "We may update these terms. If a change is material we will ask you to accept the new version in "
                + "the app before you carry on using it. The effective date above tells you which version you "
                + "are reading."),

            new("10. Contact",
                $"Questions about these terms: {ContactEmail}.")
        ]);

    public static readonly LegalDocument Privacy = new(
        Title: "Privacy Policy",
        EffectiveDate: EffectiveDate,
        Summary:
        $"This policy explains what {AppBrand.Name} collects, why, who sees it, and what you can do about it. It "
        + "covers the app and the service behind it.",
        Clauses:
        [
            new("1. What we collect",
                "Account details you give us: your email address, your first and last name, and optionally your "
                + "phone number. Content you create: your services, availability, appointments, messages and "
                + "notes. Technical data needed to run the app: your device's time zone, and a push "
                + "notification token if you allow notifications. We do not collect your location, your "
                + "contacts, or your photos."),

            new("2. Why we use it",
                "To create and secure your account, to show your availability to the people you want to see it, "
                + "to schedule and remind you about appointments, to let you and the other party message each "
                + "other, and to notify you about things that concern your bookings. We do not sell your data "
                + "and we do not use it for advertising."),

            new("3. Who sees it",
                "A provider sees the name, email, phone number and appointment history of customers who book "
                + "with them or subscribe to them. A customer sees a provider's public profile, services and "
                + "free times. Nobody else sees your data except the service providers below, and our own staff "
                + "where they need it to operate or support the service."),

            new("4. Service providers we use",
                "Cloud hosting and database storage; an email delivery provider, to send confirmations and "
                + "password resets; a push notification provider, to deliver notifications to your device; and a "
                + "payment processor where payment is taken. Each receives only what it needs to do its job."),

            new("5. How long we keep it",
                "For as long as your account exists. When you delete your account we remove your profile and "
                + "strip your name, email address and phone number from records belonging to people you dealt "
                + "with, so their own appointment history stays intact without identifying you. Audit and "
                + "security logs are kept for a limited period and do not contain your address."),

            new("6. Your choices",
                "You can edit your name and phone number, change your avatar, and turn push notifications off at "
                + "the operating-system level at any time. You can delete your account from the Profile screen, "
                + "which takes effect immediately. Your email address cannot be changed, because it is what "
                + "identifies your account."),

            new("7. Your rights",
                "Depending on where you live you may have the right to access, correct, export or erase your "
                + "data, and to object to how we use it. Deleting your account exercises the erasure right "
                + "directly. For anything else, write to us and we will respond."),

            new("8. Security",
                "Passwords are stored hashed, never in plain text. Traffic between the app and the service is "
                + "encrypted in transit. Access to production data is limited to the people who need it. No "
                + "system is perfectly secure, so tell us promptly if you think your account has been misused."),

            new("9. Children",
                "The app is not intended for children. Do not create an account if you are under the age at "
                + "which you can consent to this policy where you live."),

            new("10. Changes and contact",
                "We will update this policy when what we do changes, and the effective date above tells you "
                + $"which version you are reading. Questions, requests or complaints: {ContactEmail}.")
        ]);
}

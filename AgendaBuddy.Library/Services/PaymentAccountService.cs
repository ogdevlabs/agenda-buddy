namespace AgendaBuddy.Library.Services;

public sealed record PaymentAccountStatus(
    string Role,
    bool IsReady,
    string Status,
    string? PaymentMethodType = null,
    string? PaymentMethodBrand = null,
    string? PaymentMethodLast4 = null);

public sealed record PaymentOnboardingLink(string Url, bool CompletedLocally);

public class PaymentAccountService(
    CustomerService customers,
    ProviderService providers,
    IPaymentGateway gateway)
{
    public async Task<PaymentAccountStatus?> GetStatusAsync(string email, bool isProvider)
    {
        if (isProvider)
        {
            var provider = await providers.FindProvidersAsync(SupportTools<ProviderEntity>.FilterByEmail(email));
            if (provider is null) return null;

            if (!string.IsNullOrWhiteSpace(provider.StripeConnectedAccountId))
            {
                var state = await gateway.GetProviderPayoutStateAsync(provider.StripeConnectedAccountId);
                provider = await providers.SetPayoutStateAsync(email, state) ?? provider;
            }

            var ready = provider.StripeChargesEnabled && provider.StripePayoutsEnabled;
            return new PaymentAccountStatus(
                "Provider",
                ready,
                ready ? "Ready to receive payments" : provider.StripeDisabledReason ?? "Payout setup required");
        }

        var customer = await customers.FindCustomerAsync(SupportTools<CustomerEntity>.FilterByEmail(email));
        if (customer is null) return null;
        var paymentReady = !string.IsNullOrWhiteSpace(customer.StripeCustomerId)
            && !string.IsNullOrWhiteSpace(customer.StripeDefaultPaymentMethodId);
        return new PaymentAccountStatus(
            "Customer",
            paymentReady,
            paymentReady ? "Payment method ready" : "Payment method required",
            customer.PaymentMethodType,
            customer.PaymentMethodBrand,
            customer.PaymentMethodLast4);
    }

    public async Task<PaymentOnboardingLink?> BeginCustomerSetupAsync(
        string email, string successUrl, string cancelUrl)
    {
        var customer = await customers.FindCustomerAsync(SupportTools<CustomerEntity>.FilterByEmail(email));
        if (customer is null) return null;

        var session = await gateway.CreateCustomerSetupSessionAsync(
            email, customer.StripeCustomerId, successUrl, cancelUrl);
        await customers.SetPaymentCustomerAsync(email, session.CustomerId);

        if (session.CompletedLocally)
        {
            var method = await gateway.CompleteCustomerSetupAsync(session.SessionId, session.CustomerId);
            await customers.SetPaymentMethodAsync(email, session.CustomerId, method);
        }

        return new PaymentOnboardingLink(session.Url, session.CompletedLocally);
    }

    public async Task<PaymentAccountStatus?> CompleteCustomerSetupAsync(string email, string sessionId)
    {
        var customer = await customers.FindCustomerAsync(SupportTools<CustomerEntity>.FilterByEmail(email));
        if (customer?.StripeCustomerId is null) return null;

        var method = await gateway.CompleteCustomerSetupAsync(sessionId, customer.StripeCustomerId);
        await customers.SetPaymentMethodAsync(email, customer.StripeCustomerId, method);
        return await GetStatusAsync(email, false);
    }

    public async Task CompleteCustomerSetupByStripeCustomerAsync(string stripeCustomerId, string sessionId)
    {
        var customer = await customers.FindCustomerByStripeCustomerIdAsync(stripeCustomerId);
        if (customer?.Email is null) return;
        await CompleteCustomerSetupAsync(customer.Email, sessionId);
    }

    public async Task<PaymentOnboardingLink?> BeginProviderOnboardingAsync(
        string email, string returnUrl, string refreshUrl)
    {
        var provider = await providers.FindProvidersAsync(SupportTools<ProviderEntity>.FilterByEmail(email));
        if (provider is null) return null;

        var session = await gateway.CreateProviderOnboardingSessionAsync(
            email, provider.StripeConnectedAccountId, returnUrl, refreshUrl);
        await providers.SetConnectedAccountAsync(email, session.AccountId);

        if (session.CompletedLocally)
        {
            var state = await gateway.GetProviderPayoutStateAsync(session.AccountId);
            await providers.SetPayoutStateAsync(email, state);
        }

        return new PaymentOnboardingLink(session.Url, session.CompletedLocally);
    }
}

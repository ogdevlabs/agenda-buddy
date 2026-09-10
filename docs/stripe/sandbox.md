# Stripe Sandbox Setup

This runbook configures real Stripe test APIs without moving money. Use a Stripe sandbox or test mode and only
`sk_test_...` credentials. Never enter a real card number in a sandbox.

## 1. Prerequisites

- Access to a Stripe account that can create sandboxes and use Connect.
- Stripe CLI installed for local webhook forwarding: `brew install stripe/stripe-cli/stripe`.
- AgendaMe local prerequisites from the repository README, including AppHost user secrets and a container runtime.
- Two AgendaMe test identities: one Customer and one Provider.
- A paid fixed or hourly service owned by the Provider.

The anonymous claimable Stripe sandbox is insufficient for this complete runbook because it can reject Accounts
API calls. Use an owned sandbox with Connect access.

## 2. Create and configure the sandbox

1. In the Stripe Dashboard account picker, create or select a sandbox.
2. Confirm the Dashboard is in the sandbox before copying any key.
3. Open **Developers > API keys** and reveal the sandbox secret key. It must start with `sk_test_`.
4. Open **Connect > Settings** and activate the platform profile.
5. Select Express connected accounts and complete any platform business-profile prompts.
6. Enable the `card_payments` and `transfers` capabilities for countries where test providers will onboard.
7. Configure platform branding and support details. Stripe-hosted Checkout and Express onboarding display them.
8. Leave live mode untouched.

Do not enable untested payment methods merely because they appear in the Dashboard. The current release is
certified for cards through hosted setup Checkout; wallet and PayPal activation remains separate work.

## 3. Configure local AppHost secrets

Set the sandbox API key on the AppHost project. The command writes macOS/.NET user secrets, not repository files:

```bash
dotnet user-secrets set "Parameters:stripe-api-key" "sk_test_REPLACE_ME" \
  --project AgendaBuddy.AppHost/AgendaBuddy.AppHost.csproj
```

Authenticate the Stripe CLI in the same sandbox:

```bash
stripe login
```

Start a webhook forwarder to the local Gateway. Restricting events keeps the handler load and test output useful:

```bash
stripe listen \
  --events checkout.session.completed,payment_intent.amount_capturable_updated,payment_intent.requires_action,payment_intent.processing,payment_intent.succeeded,payment_intent.canceled,payment_intent.payment_failed,refund.created,refund.updated,charge.dispute.created \
  --forward-to http://localhost:6080/api/v1/payments/stripe/webhook
```

Copy the `whsec_...` value printed by `stripe listen`, then store it:

```bash
dotnet user-secrets set "Parameters:stripe-webhook-secret" "whsec_REPLACE_ME" \
  --project AgendaBuddy.AppHost/AgendaBuddy.AppHost.csproj
```

The CLI signing secret is listener-specific and can change when a listener is recreated. Update the user secret and
restart AppHost whenever it changes. Do not use a Dashboard endpoint secret for CLI-forwarded events.

## 4. Start AgendaMe

```bash
export PATH="$HOME/.rd/bin:$PATH"
dotnet run --project AgendaBuddy.AppHost
```

Keep the Stripe listener running in a second terminal. In the Aspire dashboard, verify:

1. Booking reaches `Running` and `/health` is healthy.
2. Booking has the two Stripe parameter references; do not reveal or paste their values.
3. Gateway is available at `http://localhost:6080`.
4. Booking does not log the `Payments:Stripe:ApiKey` unavailable warning.

If AppHost started before both secrets existed, stop and restart it so its resource graph includes both parameters.

## 5. Verify customer payment setup

1. Sign in as the Customer in the mobile app.
2. Open **Profile > Payment method**, or complete the post-registration payment step.
3. Start setup. AgendaMe calls `POST /api/v1/booking/payments/customer/setup` and opens Stripe-hosted Checkout.
4. Enter Stripe's Visa test card `4242 4242 4242 4242`, any future expiry and any three-digit CVC.
5. Complete Checkout. Stripe returns through the Gateway and then `agendame://payment-method/complete`.
6. Confirm the app reports the payment method ready and shows Visa plus last four `4242`.
7. In Stripe, verify one Customer, a succeeded SetupIntent and an attached reusable PaymentMethod.

AgendaMe must not store a PAN, CVC or expiry. MongoDB should contain only Stripe IDs and masked method metadata.

## 6. Verify provider onboarding

1. Sign in as the Provider.
2. Open **Profile > Payout account**, or complete the post-registration payout step.
3. Start onboarding. AgendaMe calls `POST /api/v1/booking/payments/provider/onboarding` and opens an Express
   Account Link.
4. Complete Stripe's test onboarding with Stripe-provided test identity and bank values for the selected country.
5. Return to AgendaMe through `agendame://provider-payout/complete`.
6. Refresh the payout screen. It is ready only when both `charges_enabled` and `payouts_enabled` are true.
7. In Stripe Connect, verify the Express account has `card_payments` and `transfers` active.

Account Links are single-use and expire. If onboarding is abandoned or a link expires, use Refresh/Create link in
the app rather than reusing the old URL.

## 7. Verify the full payment lifecycle

Use an appointment close enough to confirm but no more than four days before its scheduled time.

1. As the Customer, request the Provider's paid fixed/hourly service.
2. Verify the appointment records the server-calculated amount and currency; no Stripe charge exists yet.
3. As the Provider, confirm the appointment.
4. In Stripe, find the PaymentIntent by metadata `appointment_identifier`.
5. Verify:
   - status is `requires_capture`;
   - capture method is manual;
   - destination is the Provider's `acct_...`;
   - application fee is exactly 10% of gross in minor units;
   - the remainder allocated to the provider is exactly 90%.
6. Exercise one completion path: mark the appointment Completed and verify the intent reaches `succeeded` or
   `processing` and the AgendaMe payment ledger reaches Succeeded.
7. Exercise one pre-capture cancellation path on another appointment: Provider cancels and the intent reaches
   `canceled`; the appointment is cancelled only after the release succeeds.
8. Exercise one post-capture cancellation/refund path on another appointment: Provider cancels after capture and
   verify a 100% customer refund, transfer reversal and application-fee refund.

## 8. Exercise failure and reconciliation paths

At minimum, run these sandbox cases:

| Case | Stripe test value/action | Expected AgendaMe behavior |
| --- | --- | --- |
| Successful reusable card | `4242 4242 4242 4242` | Setup ready; authorization reaches Authorized |
| Decline after attachment | `4000 0000 0000 0341` | Setup can succeed; provider confirmation does not move appointment to Booked |
| Duplicate webhook | Stripe Workbench **Resend** on a delivered event | HTTP 200; event ID is processed once |
| Invalid signature | POST without a valid `Stripe-Signature` | HTTP 400 and no state change |
| Missing webhook configuration | Temporarily remove local webhook secret and restart | Webhook route returns 404 |
| Expired/abandoned onboarding link | Reopen an old Account Link | Generate a fresh link; no provider readiness is fabricated |
| Provider cancellation before capture | Cancel an authorized appointment | Intent canceled before appointment status changes |
| Provider cancellation after capture | Cancel a captured appointment | Full refund and both fee/transfer reversals |

Use Workbench **Webhooks > Event deliveries** to confirm HTTP 200 responses. AgendaMe stores webhook processing
leases in `stripe_webhook_events`; a failed handler can reclaim an incomplete lease after five minutes.

## 9. Configure a deployed sandbox

For a shared staging/test deployment, use that environment's Azure Key Vault and its own Stripe sandbox. Do not
reuse production Stripe credentials or webhook endpoint.

```bash
az keyvault secret set \
  --vault-name "<sandbox-key-vault>" \
  --name "stripe-api-key" \
  --value "sk_test_REPLACE_ME"

az keyvault secret set \
  --vault-name "<sandbox-key-vault>" \
  --name "stripe-webhook-secret" \
  --value "whsec_REPLACE_ME"
```

Register this HTTPS destination in the same Stripe sandbox:

```text
https://<sandbox-gateway-host>/api/v1/payments/stripe/webhook
```

Choose **Your account**, **Snapshot events**, and only these event types:

- `checkout.session.completed`
- `payment_intent.amount_capturable_updated`
- `payment_intent.requires_action`
- `payment_intent.processing`
- `payment_intent.succeeded`
- `payment_intent.canceled`
- `payment_intent.payment_failed`
- `refund.created`
- `refund.updated`
- `charge.dispute.created`

Reveal that destination's `whsec_...` only after creating it and store it in Key Vault. Then run **Deploy to Azure
Container Apps** for the target GitHub Environment. Use `provision: false` for application/secret refreshes and
`provision: true` only for the environment's first deployment or an infrastructure change.

After deployment, repeat sections 5-8 against the deployed Gateway and check the deployment smoke test, Stripe
event deliveries, Booking logs and MongoDB ledger state.

## 10. Reset the sandbox

Delete sandbox Customers, connected accounts and test data only when no other tester depends on them. Stripe test
objects cannot be moved to live mode. To return local AgendaMe to non-charging mode:

```bash
dotnet user-secrets remove "Parameters:stripe-api-key" \
  --project AgendaBuddy.AppHost/AgendaBuddy.AppHost.csproj
dotnet user-secrets remove "Parameters:stripe-webhook-secret" \
  --project AgendaBuddy.AppHost/AgendaBuddy.AppHost.csproj
```

Restart AppHost and confirm it uses visibly synthetic `local_` payment records.

# Stripe Production Setup

Production activation enables real authorizations, captures, transfers, refunds, disputes and payouts. Complete
every gate in this runbook. Do not substitute a successful sandbox test for legal, operational or live-account
readiness.

## 1. Release prerequisites

Before enabling live keys, confirm:

- The production Stripe account is owned by the legal AgendaMe operating entity and has completed activation.
- Stripe Connect is approved for the platform's business model, countries, currencies and provider categories.
- Counsel/accounting have confirmed merchant-of-record, tax, refund, dispute, negative-balance and 90/10 fee
  treatment. The implementation uses destination charges; the platform creates the charge.
- Public Terms and Privacy text has legal approval and matches off-session authorization, provider payouts,
  cancellation and refund behavior.
- Customer support owns refund/dispute escalation and has production Stripe Dashboard access with least privilege.
- The production Gateway has a stable HTTPS hostname and TLS 1.2 or newer.
- MongoDB backups, monitoring and credential rotation are complete. The known Atlas credential-history risk must be
  closed before treating the environment as production-ready.
- The sandbox runbook passed end to end, including decline, duplicate webhook, release and full-refund cases.
- Beads issue `agenda-buddy-83z` is resolved for every wallet/payment rail included in the launch claim.

## 2. Activate the live Stripe account

1. Switch the Stripe Dashboard to live mode.
2. Complete business identity, representative, bank, tax and support information.
3. Configure public business name, statement descriptor, support email/phone and customer-facing branding.
4. Activate Connect and select Express connected accounts.
5. Request `card_payments` and `transfers` for each supported provider country.
6. Review responsibility for Stripe fees, negative balances, disputes and connected-account losses against the
   destination-charge commercial agreement.
7. Configure payout schedules and minimum balances deliberately; do not inherit sandbox assumptions.
8. Enable only payment methods certified by the release checklist. Dashboard availability alone is not approval.
9. Configure Radar rules and review thresholds. Record who can override a block and how it is audited.
10. Configure Dashboard team roles, mandatory MFA and at least two break-glass administrators.

The application currently creates Accounts v1 Express accounts and requests `card_payments` plus `transfers`. A
Stripe account configured only for Accounts v2 is not a drop-in replacement.

## 3. Create restricted live credentials

Prefer a restricted key that grants only the resources used by AgendaMe. Validate it in a production-like sandbox
before replacing the live secret. At minimum, the integration needs to create/read/update operations for:

- Customers
- Checkout Sessions in setup mode
- SetupIntents
- PaymentMethods
- Express Accounts and Account Links
- PaymentIntents, captures and cancellations
- Refunds

If Stripe cannot express the necessary Connect permissions in a restricted key for this account, use the live
secret key temporarily, document the exception, restrict vault access and schedule migration. A claimable sandbox
key is not suitable because it can deny Accounts API access.

Copy the live key once. It must start with `sk_live_`. Never place it in shell history, chat, tickets or source. Use
an approved secure workstation and write it directly to the production Key Vault.

## 4. Register the production webhook

In live-mode Stripe Workbench:

1. Open **Webhooks > Create an event destination**.
2. Select **Your account**. Destination charges, platform Customers and Checkout Sessions belong to the platform
   account.
3. Select **Snapshot events**.
4. Set the endpoint URL:

   ```text
   https://<production-gateway-host>/api/v1/payments/stripe/webhook
   ```

5. Select only:
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
6. Create the endpoint, reveal its signing secret and immediately store it in the production Key Vault. It must
   start with `whsec_`.
7. Do not reuse the sandbox endpoint secret. Signing secrets are unique per endpoint and mode.

Provider status does not depend solely on `account.updated`: AgendaMe retrieves the connected account whenever the
provider payment-account status is read. Do not add a second connected-account webhook destination until the app
supports and rotates a separate signing secret for it.

## 5. Store production secrets

Identify the Key Vault owned by the production GitHub Environment/Terraform state. Authenticate with a role that
can set secrets without reading unrelated values.

Avoid typing secret values directly into shell commands, where they enter shell history. This pattern reads each
value without echo and keeps the command itself free of the literal secret (the value can still be briefly visible
to a privileged local process through the command's argument list):

```bash
printf 'Stripe live API key: '
IFS= read -r -s STRIPE_API_KEY
printf '\n'
az keyvault secret set \
  --vault-name "<production-key-vault>" \
  --name "stripe-api-key" \
   --value "$STRIPE_API_KEY" >/dev/null
unset STRIPE_API_KEY

printf 'Stripe webhook signing secret: '
IFS= read -r -s STRIPE_WEBHOOK_SECRET
printf '\n'
az keyvault secret set \
  --vault-name "<production-key-vault>" \
  --name "stripe-webhook-secret" \
   --value "$STRIPE_WEBHOOK_SECRET" >/dev/null
unset STRIPE_WEBHOOK_SECRET
```

Confirm existence and enabled state without printing values:

```bash
az keyvault secret show --vault-name "<production-key-vault>" \
  --name "stripe-api-key" --query '{enabled:attributes.enabled,updated:attributes.updated}'
az keyvault secret show --vault-name "<production-key-vault>" \
  --name "stripe-webhook-secret" --query '{enabled:attributes.enabled,updated:attributes.updated}'
```

The deployment workflow maps these names to AppHost parameters and injects them only into Booking. Do not add
parallel GitHub secrets or plaintext Container App settings; that creates an untracked second source of truth.

## 6. Deploy with a controlled window

1. Announce the payment activation window and freeze unrelated production changes.
2. Confirm the current `main` commit and release tag passed backend, integration, mobile and container gates.
3. Confirm the production GitHub Environment has required Azure OIDC variables and approvals.
4. Run **Deploy to Azure Container Apps** with the production environment name.
5. Use `provision: false` when only refreshing application images or Key Vault-backed parameters.
6. Use `provision: true` only for first deployment or a reviewed infrastructure/AppHost graph change.
7. Require a successful Gateway `/health` smoke test before proceeding.
8. Verify Booking starts without the payment-unavailable warning. Never print environment secret values.

The deployment may restart Booking. Existing Stripe objects remain in Stripe/MongoDB and webhook retries are
deduplicated by event ID.

## 7. Production canary

Use designated internal accounts and the smallest permitted real amount. Stripe prohibits real card details in
test mode; conversely, production canary transactions must use authorized real payment instruments and must be
refunded according to company policy.

Run in this order:

1. **Customer setup:** save an internal customer's card through hosted Checkout. Verify only masked metadata is
   visible in AgendaMe.
2. **Provider onboarding:** onboard an internal legal provider and bank account. Verify both charges and payouts are
   enabled.
3. **Authorization:** request and confirm one low-value appointment. Verify `requires_capture`, destination account,
   exact gross amount and 10% application fee.
4. **Release:** provider-cancel that appointment before capture. Verify full authorization release and no transfer.
5. **Capture:** authorize a second low-value appointment and complete it. Verify capture, 90/10 allocation and
   ledger state.
6. **Refund:** provider-cancel the captured canary if policy permits. Verify 100% refund, transfer reversal and
   application-fee refund.
7. **Webhook:** in Workbench, confirm every event delivery returns HTTP 200 and no retries remain pending.
8. **Payout:** verify the connected account balance/payout schedule; do not accelerate payout solely for canary
   convenience.

Record Stripe object IDs, AgendaMe appointment IDs, timestamps and results in the restricted release evidence. Do
not record payment credentials or full personal/bank data.

## 8. Go-live checks

Before opening payments broadly:

- Confirm the webhook endpoint is Enabled and has no failed deliveries.
- Confirm Booking logs have no Stripe authentication, permission, signature or destination-account errors.
- Confirm provider onboarding is limited to supported countries and service categories.
- Confirm customer support can locate a payment by `appointment_identifier` metadata.
- Confirm finance can reconcile gross amount, Stripe fees, 10% application fee, provider transfer and refund.
- Confirm alerts exist for webhook 4xx/5xx, Booking 5xx, payment authorization failures, disputes and payout
  failures.
- Confirm the app does not claim Apple Pay, Google Pay or PayPal unless each rail passed physical-device and live
  domain/account verification.
- Confirm the release owner explicitly authorizes traffic expansion.

## 9. Monitoring and incident response

### Daily checks during launch

- Stripe Workbench webhook delivery success and retry queue.
- PaymentIntent statuses stuck outside the expected lifecycle.
- AgendaMe payment records in Failed, RequiresAction or Disputed.
- Connected accounts with disabled charges/payouts or new requirements.
- Refund, transfer-reversal and application-fee-refund outcomes.
- Disputes, Radar blocks, negative balances and payout failures.

### Common signals

| Signal | Likely cause | First action |
| --- | --- | --- |
| Webhook 404 | Signing secret absent/sentinel or wrong Gateway route | Verify Key Vault secret exists, deploy completed, and endpoint URL is exact |
| Webhook 400 | Wrong endpoint secret, mixed sandbox/live mode, stale clock or modified raw body | Compare endpoint mode/secret; inspect delivery signature failure; verify NTP |
| Stripe 401 | Invalid/revoked API key | Roll key and redeploy; do not retry with logged plaintext |
| Stripe 403/permission error | Restricted key lacks required resource or Connect access | Review key permissions and platform activation |
| Provider not ready | Incomplete KYC, missing external account, disabled capability | Open provider payout status and follow Stripe requirements |
| Confirmation remains Requested | Authorization failed or requires customer action | Inspect PaymentIntent and payment ledger; do not force Booked |
| Cancellation remains active | Stripe release/refund failed | Resolve Stripe failure and retry cancellation; do not manually hide the appointment |
| Duplicate delivery | Normal Stripe retry/manual resend | Confirm one completed `stripe_webhook_events` record and HTTP 200 |

Stripe can deliver duplicates and events out of order. Do not delete webhook deduplication records as a first-line
incident response.

## 10. Secret rotation

### API key

1. Create the replacement live key with the required permissions.
2. Validate it against non-mutating reads and, if policy allows, a controlled canary.
3. Write the replacement to Key Vault as `stripe-api-key`.
4. Deploy Booking and pass health plus payment-account status checks.
5. Complete one authorization/release canary.
6. Revoke the previous key in Stripe.
7. Record rotation evidence without secret values.

### Webhook signing secret

The application currently accepts one webhook secret, so it cannot validate old and new endpoint secrets
simultaneously. Use Stripe's delayed expiration window:

1. In Workbench, choose **Roll secret** and delay old-secret expiration.
2. Immediately store the new `whsec_...` in Key Vault.
3. Deploy Booking.
4. Send/resend a live-safe event and confirm HTTP 200 with the new secret.
5. Expire the old secret only after successful validation.

If zero-downtime dual-secret rotation becomes mandatory, add multi-secret verification before changing this
procedure.

## 11. Disable or roll back payments

Use this when credentials are compromised, Stripe behavior is unsafe, or reconciliation cannot be trusted.

1. Disable the Stripe webhook endpoint if forged/replayed traffic is suspected; preserve delivery/event evidence.
2. Revoke the compromised API key in Stripe.
3. Remove `stripe-api-key` and `stripe-webhook-secret` from the environment Key Vault, or replace both with reviewed
   values.
4. Redeploy. The workflow supplies the non-empty `__UNCONFIGURED__` sentinel for absent Stripe secrets.
5. Verify non-local payment mutations fail closed and the webhook route returns 404.
6. Do not mark pending appointments Booked, Completed or Cancelled by direct database edits.
7. Reconcile every existing `requires_capture`, captured, refunding and disputed Stripe object before resuming.
8. Restore service only after a fresh canary and finance/support approval.

Removing credentials stops new Stripe operations but does not cancel existing authorizations, refund captured
payments, resolve disputes or stop Stripe payouts. Those objects require explicit operational handling in Stripe.

## 12. Final production evidence

Retain these non-secret artifacts:

- Approved Stripe account/Connect activation and supported-country list.
- Webhook endpoint ID, subscribed event list and successful delivery timestamps.
- Release commit/tag and successful deployment run URL.
- Canary appointment, PaymentIntent, refund and connected-account IDs.
- Evidence of exact 90/10 allocation and 100% provider-cancellation refund/release.
- Monitoring dashboard and alert ownership.
- Security/legal/finance/support sign-offs.
- Secret creation/rotation timestamps and owners, never values.

Production activation is complete only when all evidence exists and the remaining external-rails issue is closed or
explicitly narrowed to methods not advertised by the release.

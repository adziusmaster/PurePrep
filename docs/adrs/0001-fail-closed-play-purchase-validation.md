# 1. Fail-closed Google Play purchase validation

Date: 2026-08-27

Status: Accepted

## Context

The server granted Smart Credits in response to `POST /api/billing/redeem`. Validation of the
purchase token was delegated to `IPlayValidator`, which had exactly one implementation:
`DevPlayValidator`, returning `Valid = true` for any non-empty string.

That implementation was registered unconditionally in the composition root. Both the registration
comment ("real validator in Production") and the class documentation ("NEVER selected when running
in Production") asserted otherwise, so the defect was invisible on reading either one in isolation.

The consequence was that anyone able to send an HTTP request could mint unlimited credits, and the
backend URL is a plain string constant in the shipped APK. Every AI import costs real money against
a Gemini API key, so this was a direct, unbounded financial exposure.

A second, smaller hole reinforced it: device identity is a client-generated GUID with no
attestation, and the server seeded free credits for any GUID it had not seen before.

## Decision

1. Purchase validation calls the Google Play Developer API (`androidpublisher` v3). Credits are
   granted only when Google reports the purchase as `Purchased`, not yet consumed, and carrying an
   order id.
2. **The composition root refuses to start in Production when Play credentials are absent or
   unreadable.** Selection is a pure function, `PlayValidatorSelection.Select`, covered by tests
   that assert the production-without-credentials case throws.
3. Validation failures — including Google being unreachable — resolve to *invalid*. An outage must
   not become a way to mint credits.
4. Free-credit seeding is capped per origin, keyed on a salted hash of the client IP rather than
   the address itself.

## Consequences

### Positive

- Forged purchase tokens no longer grant credits; an integration test asserts this against the real
  endpoint and routing.
- The dangerous state is now unreachable by configuration rather than by convention. A misconfigured
  deployment fails loudly at startup instead of silently accepting forgeries.
- The rule lives in a tested pure function, so it cannot drift from the comment describing it — which
  is precisely how the original defect survived.
- The seed cap contains free-credit farming without introducing accounts or device attestation.

### Negative

- Production now has a hard dependency on a Google service-account key. A missing or expired key is
  an outage rather than a degraded mode. This is deliberate — the degraded mode was the vulnerability
  — and `docs/play-validation-setup.md` documents provisioning and rotation.
- Google permission propagation can take up to 24 hours, during which validation fails closed and no
  credits are granted. Purchases are not lost: the client leaves them un-consumed and retries.
- The seed cap can deny free credits to genuine users behind a large shared NAT. The cap is
  configurable via `Credits:MaxNewDevicesPerIpPerDay`, and such users can still buy or redeem a code.
- Requests with no determinable client IP receive no free allowance. Behind Kestrel an address is
  always present; this fails closed rather than open if forwarded headers are ever misconfigured.

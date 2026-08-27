# Setting up Google Play purchase validation

The server verifies every purchase with Google before granting credits. It **will not start in
Production** until this is configured — without it, any forged purchase token would be accepted as
proof of payment.

You need to do this once. Budget about 20 minutes, plus up to 24 hours for Google to propagate the
new permissions.

---

## 1. Enable the API and create a service account

1. Open the [Google Cloud console](https://console.cloud.google.com/) with the same Google account
   that owns the Play Console developer profile.
2. Create a project (or pick an existing one). Note its name.
3. Go to **APIs & Services → Library**, search for **Google Play Android Developer API**, open it
   and press **Enable**.
4. Go to **IAM & Admin → Service Accounts → Create service account**.
   - Name: `pureprep-play-validator`
   - Skip the optional role and user-access steps — permissions are granted in the Play Console,
     not here.
5. Open the new service account → **Keys → Add key → Create new key → JSON**. A `.json` file
   downloads. **This is the credential. Treat it like a password.**
6. Copy the service account's email address (it looks like
   `pureprep-play-validator@<project>.iam.gserviceaccount.com`).

## 2. Link it in the Play Console

1. Open the [Play Console](https://play.google.com/console/) → **Users and permissions → Invite new
   users**.
2. Paste the service account email address.
3. Under **App permissions**, add **PurePrep**.
4. Under **Account permissions**, grant only:
   - **View financial data, orders, and cancellation survey responses**
   - **Manage orders and subscriptions**

   These are the two the validator needs. Do not grant release or publishing permissions.
5. **Invite user**, then confirm.

> Permission changes can take up to 24 hours to take effect. Until then the API returns
> `401`/`403` and the server treats purchases as unvalidated — which fails closed, so no credits
> are granted. Purchases made during that window are not lost: the app leaves them un-consumed and
> retries on the next buy.

### Check whether the link is live yet

**Do this before installing the key on the server.** Deploying while permissions are still
propagating means real customers are charged and receive nothing.

```bash
./deploy/verify-play-access.sh /path/to/service-account.json
```

| Result | Meaning |
|---|---|
| `READY` | Google answered for this app. Safe to deploy. |
| `NOT READY` | The key is valid but has no access to the app yet — still propagating, or the Play Console grant is incomplete. Re-run later. |
| `BAD KEY` | Google rejected the credential. Create a fresh JSON key. |

Re-run it until it says `READY`.

## 3. Install the key on the server

```bash
# On the Hetzner host
sudo mkdir -p /opt/pureprep/secrets
sudo cp ~/Downloads/<downloaded-key>.json /opt/pureprep/secrets/play-service-account.json
sudo chmod 600 /opt/pureprep/secrets/play-service-account.json
```

Then add to `/opt/pureprep/.env`:

```bash
PLAY_KEY_PATH=/opt/pureprep/secrets/play-service-account.json
PLAY_PACKAGE_NAME=com.adziusmaster.pureprep

# Generate once, then never change it:
IP_HASH_SALT=<output of: openssl rand -hex 32>
```

Redeploy:

```bash
cd /opt/pureprep
docker compose --env-file .env -f deploy/docker-compose.prod.yml up -d --build
```

## 4. Verify the deployment

```bash
# Should return {"status":"ok"} — if the container is not running, the key is missing or unreadable.
curl -s https://api.pureprep.lechdigital.nl/health

# A forged token must be rejected. Expect HTTP 400, and no credits granted.
curl -s -o /dev/null -w '%{http_code}\n' \
  -X POST https://api.pureprep.lechdigital.nl/api/billing/redeem \
  -H 'Content-Type: application/json' \
  -d '{"deviceId":"00000000-0000-0000-0000-000000000001","productId":"credits_10","purchaseToken":"forged"}'
```

If the container fails to start, check the logs — the startup error names the missing setting
explicitly:

```bash
docker compose -f deploy/docker-compose.prod.yml logs pureprep | tail -20
```

## Rotating the key

Create a second key on the same service account, replace the file, redeploy, then delete the old
key in the Cloud console. No Play Console change is needed.

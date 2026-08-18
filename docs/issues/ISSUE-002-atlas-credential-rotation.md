# ISSUE-002 — Rotate the `agenda_buddy` Atlas credential

**Status:** 🔴 **OPEN — operational, needs a human with Atlas access**
**Severity:** highest residual risk in the project · **Filed:** 2026-08-18
**Origin:** F-013 threat **T-001** / PRD **OQ-1** · **Supersedes** the blocker text carried in `docs/pdlc/memory/STATE.md`

---

## What happened

A MongoDB Atlas connection string containing a **username and password with full read/write access to
the `agenda_buddy` cluster** was committed to this repository and lived in **17 tracked files** —
`appsettings*.json` across the services, plus two files under `docs/pdlc/context/` that a
documentation backfill had copied it into.

F-013 removed it from every tracked file and CI now asserts it cannot come back
(`.github/workflows/dotnet.yml`, "Assert no committed database credential").

## Why removal is not remediation

**The credential is still in git history and still works.** Deleting a secret from the working tree
changes nothing about its validity:

- `git log -p`, any existing clone, any fork, and every CI cache still contain it. GitHub's API will
  serve the old blob by SHA even after a force-push.
- Anyone who cloned or fetched this repository at any point before F-013 merged has it on disk.
- The repository is on GitHub. Assume automated secret scanners — the friendly and the unfriendly kind
  — have already harvested it. Credentials in public repositories are typically probed within
  **minutes**, not days.

There is exactly one action that ends the exposure: **change the password at Atlas.** Until then, the
only honest description of the cluster's security state is "shared with the internet".

## What it is critical for — concretely

This is not a theoretical hygiene item. The credential grants read/write to the database that holds:

| Collection | What an attacker gets |
|---|---|
| `customers` | Names, **email addresses**, phone numbers of every client of every provider |
| `providers` | Provider identities and contact details |
| `appointments` | Who met whom, when, and for what service — a behavioural record of therapy, tutoring and coaching sessions |
| `services` | Pricing and service catalogue |
| Identity data | Whatever `IdentityDb` holds on the same cluster |

The concrete consequences, in the order they would actually hurt:

1. **A personal-data breach.** Agenda Buddy's users are therapists, tutors and coaches. An
   appointment record linking a named individual to a therapist at a specific time is **sensitive
   personal data**, and the customer collection is a ready-made list for phishing or extortion. Under
   GDPR this is a notifiable personal-data breach with a **72-hour** reporting clock from the moment
   the controller becomes aware — and the clock is arguably already running, because this document is
   awareness.
2. **Silent data modification.** The credential is read **and write**. An attacker who deletes or
   quietly alters appointments does damage no read-only leak could, and the project has **no backups
   and no restore drill** (`docs/deployment.md`, item 5), so there is currently nothing to restore
   from.
3. **Credential reuse.** If that password is used anywhere else — another cluster, another
   environment, a personal account — the blast radius is larger than this project.
4. **It blocks the cloud deployment.** Deploying to Azure Container Apps against this cluster means
   the deployment and whoever else holds the credential share a database. The deployment doc lists
   rotation as the first prerequisite for exactly this reason.
5. **Every downstream security control is theatre until it is done.** F-013 added PII redaction in
   traces (T-004), JWT masking (T-003) and a CI credential guard. All of that protects the front door
   while a copy of the key is on the pavement outside.

## What to do

1. **Rotate at Atlas.** Atlas UI → Database Access → the `agenda_buddy` user → *Edit* → *Edit
   Password* → *Autogenerate Secure Password* → update. Or `atlas dbusers update <user> --password`.
   Do this first; everything else is cleanup.
2. **Review the Atlas access log for the whole exposure window** — Atlas UI → Project → *Activity
   Feed*, and the cluster's *Access Manager* / database access history. Look for source IPs that are
   not a developer machine or CI, connections outside working hours, and unexpected authentication
   successes. **The exposure window starts at the first commit that contained the credential**, not at
   the date F-013 removed it:

   ```bash
   git log --diff-filter=A --format='%h %ad %s' --date=short -S 'mongodb+srv://' -- '*.json' | tail -5
   ```

3. **Restrict network access while you are there.** Atlas → Network Access. If the IP access list is
   `0.0.0.0/0`, the credential was the only thing standing between the internet and the data. Narrow
   it to known egress addresses.
4. **Update the consumers** with the new password: each developer's user secrets, and the
   `AZD_ENV_VARS` secret once anything is deployed. Nothing in the repository needs editing — that is
   the point of F-013.
5. **Decide about history.** Rotation makes the leaked value worthless, which is sufficient. Rewriting
   history with `git filter-repo` additionally removes it, at the cost of invalidating every clone and
   every existing PR. **Rotate first regardless** — history rewriting is optional, rotation is not.
6. **Record the outcome here**, including whether the access log showed anything, and close this
   issue. If the log cannot be reviewed as far back as the exposure window, say so explicitly rather
   than implying a clean review.

## Why this is still open

It needs Atlas console access and a judgement call about the access log — neither of which an agent
should perform on someone's production data. It has been documented in five places across F-013 and
carried in `STATE.md` as the highest-severity blocker since 2026-08-17; **documenting it again is not
progress.** The only thing that closes it is the rotation.

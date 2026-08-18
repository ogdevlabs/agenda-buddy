# ISSUE-002 — Rotate the `agenda_buddy` Atlas credential

**Status:** 🔴 **OPEN — operational, needs a human with Atlas access**
**Severity:** highest residual risk in the project · **Filed:** 2026-08-18
**Origin:** F-013 threat **T-001** / PRD **OQ-1** · **Supersedes** the blocker text carried in `docs/pdlc/memory/STATE.md`

> ## ⚠️ CORRECTION — 2026-08-18: the data-exposure claims below are WRONG
>
> **The maintainer has confirmed the `agenda_buddy` cluster contains only synthetic / development
> data. It has never held records for real people.** Every statement in this issue about client
> names, email addresses, phone numbers, a notifiable personal-data breach, or a 72-hour GDPR clock
> is therefore **incorrect** and should not be relied on. Those claims originated in earlier PDLC
> sessions that inferred the cluster's contents from the *schema* rather than verifying them.
>
> **Severity re-graded: CRITICAL → MEDIUM.** Not "no longer a problem" — the rest of this issue
> stands, and rotation is still required:
>
> - The credential is **still valid** and grants **full read/write to a live cluster**.
> - Verified 2026-08-18: it is recoverable from **published** history — 9 commits reachable from
>   `origin/main`, earliest `ddb23ba`, with the literal still extractable from
>   `Calendar/appsettings.Development.json` at that commit. The repository is **public**.
> - **There are no backups.** Anyone with the credential can destroy the development dataset.
> - It permits Atlas resource abuse (storage, compute, egress) billed to the project owner.
> - It is a valid credential into a live Atlas project, so its blast radius is bounded by that
>   project's configuration, not by this database alone.
>
> What changed is the *kind* of risk, not its existence: this is an operational and cost/integrity
> risk, **not** a personal-data breach. There is no regulator clock and no notification duty.
>
> **Still do:** rotate the password at Atlas, and review the access log. The review window is the
> full public lifetime of `ddb23ba`, not merely since F-013.


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

1. ~~**A personal-data breach.**~~ **STRUCK 2026-08-18 — this was wrong.** The reasoning below was
   inferred from the *schema* (a `customers` collection with name/email/phone fields implies real
   people) rather than from the cluster's actual contents. The maintainer has confirmed the cluster
   holds **only synthetic/development data**. There is **no personal-data breach, no sensitive-data
   exposure, no notification duty and no 72-hour clock.**
   ~~Agenda Buddy's users are therapists, tutors and coaches. An appointment record linking a named
   individual to a therapist at a specific time is sensitive personal data, and the customer
   collection is a ready-made list for phishing or extortion.~~
   **Lesson worth keeping:** a schema tells you what data *could* be there, not what is. This document
   asserted the more alarming reading for three weeks without anyone checking.
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

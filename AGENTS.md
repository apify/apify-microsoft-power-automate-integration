# CLAUDE.md

## Project Purpose

Source of truth for the **Apify custom connector for Microsoft Power Automate**. The connector lets Power Automate flows run Apify Actors and tasks, read datasets and key-value stores, and start flows when runs finish.

Apify is a **verified publisher** (not an independent publisher), so the connector is deployed with `paconn` and submitted to Microsoft Partner Center for certification. There is no application to build here — the repo *is* the connector definition.

## Repository Structure

| Path | Description |
|------|-------------|
| `apiDefinition.swagger.json` | OpenAPI/Swagger **2.0** definition. Host `api.apify.com`, basePath `/v2`. Defines every action, trigger, and internal dropdown/schema operation. |
| `apiProperties.json` | Connector metadata: OAuth 2.0 connection parameters, `iconBrandColor`, `policyTemplateInstances`, publisher/stackOwner. |
| `scripts.csx` | C# script (`Script : ScriptBase`) dispatching on `Context.OperationId` for request/response logic Swagger can't express. |
| `icon.png` | Connector icon; must satisfy Microsoft certification icon rules (see CONTRIBUTING.md). |
| `settings.json` | `paconn` settings file (placeholder `connectorId` / `environment` — fill in locally, don't commit real IDs). |
| `intro.md` | Connector documentation shipped **inside** `ConnectorPackage.zip` for certification review. |
| `scripts/validate.py` | Offline certification validator (Python 3.9+, stdlib only). Run before every push. |
| `README.md` | User-facing docs: triggers, actions, inputs, troubleshooting, FAQ. |
| `CONTRIBUTING.md` | Developer guide: paconn setup, deploy cycle, certification packaging, policy-code history. |
| `.github/workflows/validate.yml` | CI: runs `python3 scripts/validate.py` on every push (Python 3.11). |
| `.github/workflows/claude-md-maintenance.yml` | Calls reusable workflow `apify/workflows/.github/workflows/claude-md-maintenance.yml@main` on push to `main`/`master` and via `workflow_dispatch`. Distributed centrally from `apify/integrations-team` — **do not hand-edit**. |

## Technology Stack

- **OpenAPI/Swagger 2.0** — connector surface (Power Platform does not accept OpenAPI 3.x).
- **C# script (`.csx`)** on Power Platform's `ScriptBase` runtime, using `Newtonsoft.Json` (`JObject`/`JArray`).
- **Python 3.9+** — `scripts/validate.py`, stdlib only, no third-party deps.
- **`paconn`** (Power Platform Connectors CLI, `pip install paconn`) — deploy/validate.
- **PowerShell 7+** — only for Microsoft's `ConnectorPackageValidator.ps1` during certification packaging.
- **Apify API v2** — the upstream API; **OAuth 2.0** authorization code flow (`profile`, `full_api_access`).

## Build, Test & Run

There is no build step. Deploy and validate:

```bash
# One-time setup
pip install paconn
paconn login                        # device-code flow

# Validate before every push (both must be clean)
python3 scripts/validate.py                              # offline policy checks (what CI runs)
paconn validate --api-def apiDefinition.swagger.json     # Microsoft's cert Swagger Validator

# Deploy to a Power Platform environment (add --icon icon.png only when the icon changed)
paconn update -e <ENV_ID> --api-prop apiProperties.json --api-def apiDefinition.swagger.json --script scripts.csx
```

Testing is manual: after `paconn update`, re-enter the OAuth Client ID/Secret in Power Automate (**Custom Connectors → Edit → Security → OAuth 2.0 → Edit**), then run a flow per affected action/trigger. Certification packaging is documented step-by-step in `CONTRIBUTING.md`.

## Conventions

- **Commits / PRs:** Conventional-commit prefixes (`feat:`, `fix:`, `docs:`) with the PR number appended, e.g. `fix: correct hidden property type in apiProperties.json (#36)`. All changes land on `main` via PR.
- **Branches:** `<type>/power-automate-<short-description>` (e.g. `feat/power-automate-actions-run-task`, `docs/power-automate-readme-documentation`); certification fixes use the policy code, e.g. `fix/power-automate-certification-5000-2-6-2`.
- **Operation IDs** are the contract between Swagger and `scripts.csx` — the `switch` in `ExecuteAsync()` keys off them. Renaming one in Swagger requires the matching `case` update.
- **Visibility:** user-facing operations use `x-ms-visibility: important`; dropdown/schema helpers use `internal`.
- **No URLs, instructional phrases, or emojis in any `description` field** — triggers policy 5000.2.3.6. Put prose in `intro.md`/`README.md` instead.
- **Never pass `--secret` to paconn** on the command line; it leaks into shell history. No client IDs or secrets in the repo (`apiProperties.json` keeps `{{ client id }}` / `{{ client secret }}` placeholders).
- `scripts.csx` uses two-space indentation and XML doc comments (`/// <summary>`) on handlers.

## Key Notes for AI Assistants

- **Several Swagger paths are virtual** — they don't exist in the Apify API and are rewritten by `scripts.csx` before forwarding. Don't "fix" them against the Apify API reference:
  - `/actors/dropdown` → `/v2/acts` or `/v2/store`, chosen from the `actorScope` parameter (`DetermineApiPath`).
  - `/datasets/{datasetId}/itemsSchemaHelper` → `/items` (schema inferred from a sample).
  - `/key-value-stores/{storeId}/records/{recordKey}/schemaHelper` → strips `/schemaHelper`.
  - `/webhooks/task` and `/webhooks/task/{webhookId}` → `/webhooks` and `/webhooks/{webhookId}` (separate paths exist only to give the task trigger its own operation).
  - `/scrape-single-url` → `POST /v2/acts/apify~website-content-crawler/runs`.
- **Keep `policyTemplateInstances` in `apiProperties.json` populated.** Removing the `setheader` entry emptied the exported `policytemplateinstances.json` while `customizations.xml` still referenced it, causing certification failure **5000.1.1.16**. It is structurally required even when redundant at runtime.
- **Swagger must stay OpenAPI 2.0-compliant**: no duplicate/colliding paths, no empty schemas, valid `produces` MIME types. Violations surface as **5000.2.6.2**.
- **Never hand-edit files inside an exported solution zip.** Change the source in this repo, `paconn update`, then re-export from Power Apps.
- `scripts/validate.py` skips submission-package checks when `ConnectorPackage/ConnectorPackage.zip` is absent — a clean local run does not mean the package was validated.
- Query-parameter validation lives in `scripts.csx` (`GetValidationRules`), not Swagger: URL format, non-negative integers, and `waitForFinish` capped at **60** (`MAX_WAIT_FOR_FINISH`).
- Trigger webhooks are created in the user's Apify account and are **not** cleaned up automatically when a flow is deleted; webhook idempotency is handled via a SHA-256 key (`SetWebhookIdempotencyKey`).
- `settings.json` ships with placeholder `connectorId`/`environment` values — never commit real ones.

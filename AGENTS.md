# AGENTS.md

Guidance for coding agents working in this repository. `CLAUDE.md` is only an `@AGENTS.md` pointer,
so put instructions here and leave the pointer alone. `.github/workflows/agents-md-maintenance.yml`
updates this file inside each PR that changes the code.

## Project

Source of the **Apify custom connector for Microsoft Power Automate**. There is no application to
build: the repo *is* the connector definition, deployed by hand with Microsoft's `paconn` CLI and
submitted to Microsoft Partner Center for certification (Apify is a verified publisher).

- `apiDefinition.swagger.json`: the connector surface. Swagger **2.0** only (Power Platform rejects
  OpenAPI 3), host `api.apify.com`, basePath `/v2`, OAuth 2.0 security (`apify_oauth2_auth`).
- `scripts.csx`: a C# `Script : ScriptBase` that runs in Power Platform for every operation and
  rewrites requests/responses where Swagger can't express the behaviour. Uses `Newtonsoft.Json`.
- `apiProperties.json`: OAuth connection parameters, brand colour, `policyTemplateInstances`.
- `intro.md` ships inside the certification package; `README.md` is the user-facing docs;
  `CONTRIBUTING.md` has the full paconn setup, deploy cycle, packaging steps and policy-code history.

User-facing surface (`x-ms-visibility: important`): actions Run Actor, Run Task, Get Dataset Items,
Get Key-Value Store Record, Scrape Single URL; triggers Actor run finished, Actor task finished.
Everything else (dropdown lists, schema helpers, webhook deletes, `GetUserInfo`) is `internal`.
`GetUserInfo` (`/users/me`) is also the connection test (`x-ms-capabilities.testConnection`).

## Commands

```bash
python3 scripts/validate.py                              # offline cert checks; what CI runs
paconn validate --api-def apiDefinition.swagger.json     # Microsoft's cert Swagger Validator
paconn update -e <ENV_ID> --api-prop apiProperties.json --api-def apiDefinition.swagger.json --script scripts.csx
```

- `scripts/validate.py` is Python 3.9+, stdlib only; it checks file presence, the `5000.x` policy
  codes it can verify offline (icon, title, descriptions, responses, Swagger structure, HTTPS, OAuth
  placeholders) and, if present, `ConnectorPackage/ConnectorPackage.zip`. That zip is git-ignored
  (`*.zip`), so CI always skips the package checks — a green run never validates the package.
- CI (`.github/workflows/validate.yml`) runs only the validator, on push, with Python 3.11.
  Nothing compiles or tests `scripts.csx` locally or in CI; it is only exercised after
  `paconn update` in a real environment.
- `paconn` needs `pip install paconn` and `paconn login`. Add `--icon icon.png` to `update` only when
  the icon changed. **Never pass `--secret`** (leaks into shell history); after every update the
  OAuth Client ID/Secret must be re-entered in Power Automate (Custom Connectors → Edit → Security).
- Testing is manual: deploy, then run a flow for each affected action/trigger.

## Architecture

### `scripts.csx` dispatches on `operationId`

`ExecuteAsync()` is a `switch (Context.OperationId)`. Operations without special logic are listed
in the passthrough group; **an operationId that is not in the switch returns 400 "Unknown operation
ID"**, so a new Swagger operation needs a `case` too, and renaming an operationId needs the matching
`case` renamed.

`HandlePassthrough` runs query-parameter validation from `GetValidationRules()` (keyed by
operationId) and forwards the request unchanged. Rules today: `waitForFinish` 0–60
(`MAX_WAIT_FOR_FINISH`) and non-negative `timeout` for RunActor/RunTask, non-negative
`limit`/`offset` for GetDatasetItems, `url` must be absolute http(s) with a dotted host or IP for
ScrapeSingleUrl. Failures return 400 with `error.type = VALIDATION_ERROR`. Most custom handlers
`catch` every exception and fall back to passthrough, so a bug in a rewrite usually surfaces as an
odd upstream error, not a script error.

### Virtual paths

Several Swagger paths don't exist in the Apify API; the script rewrites them before forwarding.
Don't "fix" them against the Apify API reference:

- `/actors/dropdown` (`ListActorsDropdown`) → `/v2/store` when `actorScope=StoreActors`, else
  `/v2/acts` (`DetermineApiPath`); `actorScope` is stripped.
- `/datasets/{datasetId}/itemsSchemaHelper` → `/items`; `.../records/{recordKey}/schemaHelper` →
  the record itself. Both fetch real data and infer a Swagger schema from the first item
  (`InferOpenApiSchemaFromSample`); the dataset one wraps it as `type: array`. These back the
  `x-ms-dynamic-schema` of Get Dataset Items and Get Key-Value Store Record.
- `/scrape-single-url` → `POST /v2/acts/apify~website-content-crawler/runs` with a fixed one-page
  input body built from `url` and `crawler_type`. It is virtual because a path under
  `/acts/{actorId}/runs` collided with RunActor (`PartialDuplicateOperationPath`, a cert failure).
- `/webhooks/task` and `/webhooks/task/{webhookId}` → `/webhooks` and `/webhooks/{webhookId}`.
  They exist only so the task trigger gets its own operation next to `POST /webhooks`.

### Dropdowns

`actorScope` (Recently used Actors / Apify Store Actors) is a UI-only query parameter that feeds
`x-ms-dynamic-values` on the Actor pickers. `ListActorsDropdown` and `ListTasks` rewrite titles
(`title (username/name)`, `name / (actName)`) and prefix each with a zero-padded index (`001) `)
so the dropdown keeps the API's order (`FormatItems`). The
Memory enum's `displayName`s (`1) 128 MB` …) use the same trick.

### Triggers (webhooks)

Both triggers are `x-ms-trigger: single` and create an Apify webhook whose `requestUrl` is the
Power Automate callback (`x-ms-notification-url`). The four status toggles (`onRunSucceeded`,
`onRunFailed`, `onRunTimedOut`, `onRunAborted`) are query parameters that the script removes and
converts into the body's `eventTypes`; a status counts as on unless explicitly `false`, and turning
all four off returns 400. The script also sets `idempotencyKey` = SHA-256 of
`requestUrl:actorId` (or `actorTaskId`) so re-registering the same trigger doesn't create duplicate
webhooks. README tells users to remove webhooks in Apify Console by hand after deleting a flow;
don't change that claim without testing it in Power Automate.

### The integration-platform header

Every operation sends `x-apify-integration-platform: microsoft-power-automate`, both as an internal
Swagger header parameter with that default (`#/parameters/IntegrationPlatformHeader`) and via the
`setheader` policy in `apiProperties.json` (#32 observed the policy not firing at runtime, so the
Swagger parameter is the reliable path). Keep the policy even though it looks redundant: removing
it made the exported `policytemplateinstances.json` empty while `customizations.xml` still
referenced it, which failed certification with **5000.1.1.16**. `validate.py` checks it.

## Conventions and gotchas

- **Descriptions**: no URLs, emojis or non-ASCII in any `description` (policy 5000.2.3.6, checked
  by `validate.py`). Put prose and links in `intro.md` / `README.md`.
- **Swagger hygiene for cert (5000.2.6.2)**: no colliding or partially duplicate paths, no empty
  object schemas, no schema on `default` responses, every operation has a 2xx response, path
  parameters set `"x-ms-url-encoding": "single"`, summaries ≤ 80 chars, no parameter named
  `connectionId`. `validate.py` checks all of these except path collisions, which only `paconn
  validate` catches (`PartialDuplicateOperationPath`) along with more rules — run both.
- **Never hand-edit an exported solution zip.** Change the source here, `paconn update`, re-export
  from Power Apps. Packaging steps and the policy codes hit so far are in `CONTRIBUTING.md`.
- **No real IDs or secrets in the repo**: `apiProperties.json` keeps `{{ client id }}` /
  `{{ client secret }}`, and `settings.json` keeps `YOUR-CONNECTOR-ID` / `YOUR-ENVIRONMENT-ID`.
- `uiDefinition.constraints` in `apiProperties.json` use string booleans (`"true"`/`"false"`); a
  real boolean there was a bug fixed in #36.
- `scripts.csx`: two-space indent, `/// <summary>` doc comments on handlers.
- Commits: conventional prefixes with the PR number, e.g. `fix: correct hidden property type in
  apiProperties.json (#36)`. Branches: `<type>/power-automate-<description>`; certification fixes
  name the policy code (`fix/power-automate-certification-5000-2-6-2`).

# Apify Connector

Apify is a web scraping and automation platform that provides serverless computing infrastructure for running data extraction tasks called Actors. This connector enables Microsoft Power Automate users to integrate Apify's web scraping capabilities directly into their workflows. Run Actors and Tasks, fetch data from Datasets and Key-Value Stores, and create webhooks to trigger flows when scraping jobs complete.

## Prerequisites

Before using this connector, you need to set up the following:

- An Apify account. Sign up at the [Apify Console](https://console.apify.com/).

## Supported Operations

### Obtaining Credentials

The connector supports *OAuth 2.0* authentication with the following scopes only:
- `profile`: Allows the connector to view your Apify account details and profile information.
- `full_api_access`: Grants the connector complete access to all Apify API features, including running Actors, managing tasks, and accessing datasets.

When creating a connection in Power Automate, select *Sign in with Apify*. You will be redirected to Apify to authorize access to your account.

**Note**: All requests include the header `x-apify-integration-platform: microsoft-power-automate` to identify the integration platform.

### Triggers

> **Note:**  
> Currently, when you set up these triggers, the Apify connector creates a webhook on your Apify account to notify Power Automate of completed runs. However, if you turn off or delete a workflow in Power Automate, the webhook on Apify is **not automatically removed**.  
> To prevent unused webhooks from accumulating, please manually remove old webhooks from your [Apify Console](https://console.apify.com/) by navigating to the *Integration* tab of the Actor used in your trigger (*https://console.apify.com/actors/<actor_id>/integrations*).
> This is especially important if you retire or disable flows. We are working to improve this behavior so that webhook cleanup will happen automatically in the future.

#### Actor run finished Trigger

Use the *Actor run finished* trigger to automatically execute your Power Automate flow when a specific Apify Actor run completes with a selected status.

- **Authentication**: Use *Sign in with Apify* [OAuth 2.0] (scopes: `profile`, `full_api_access`).
- **Headers**: All requests include `x-apify-integration-platform: microsoft-power-automate`.
- **Inputs**:
  - `Actor Scope`: Choose between *Recently used Actors* or *From store* (Apify store Actors).
  - `Actor`: Dynamic dropdown populated with Actors based on the selected scope.
  - `Trigger On`: Select which run statuses should trigger the flow (SUCCEEDED, FAILED, TIMED_OUT, ABORTED).
- **Output**: Webhook payload containing detailed information about the completed Actor run.

How it works:
- Actor dropdown is populated via `GET /v2/acts` (for recent Actors) or via `GET /v2/store` store API (for store Actors).
- The trigger creates a webhook via `POST /v2/webhooks` that subscribes to Actor run events.
- When the selected Actor finishes with one of the specified statuses, Apify sends a webhook payload to Power Automate.

#### Actor Task Finished Trigger

Use the *Actor task finished* trigger to automatically execute your Power Automate flow when a specific Apify Actor task run completes with a selected status.

- **Authentication**: Use *Sign in with Apify* [OAuth 2.0] (scopes: `profile`, `full_api_access`).
- **Headers**: All requests include `x-apify-integration-platform: microsoft-power-automate`.
- **Inputs**:
  - `Task`: Dynamic dropdown populated with your Actor tasks.
  - `Trigger On`: Select which run statuses should trigger the flow (SUCCEEDED, FAILED, TIMED_OUT, ABORTED).
- **Output**: Webhook payload containing detailed information about the completed task run.

How it works:
- Creates a webhook via `POST /v2/webhooks` that subscribes to Actor task run events.
- Task dropdown is populated via `GET /v2/actor-tasks` to list your available tasks.

### Actions

#### Get Dataset Items Action

Use the *Get dataset items* action to retrieve records from one of your Apify datasets.

- Authentication: Use *Sign in with Apify* [OAuth 2.0] (scopes: `profile`, `full_api_access`).
- Headers: All requests include `x-apify-integration-platform: microsoft-power-automate`.
- Dataset (`datasetId`): Select a dataset from a dynamically populated dropdown of your datasets.
- Optional query params:
  - `limit`: number of items to return.
  - `offset`: number of items to skip (for pagination).
- Output: An array of dataset items. The item shape is dynamic and depends on the selected dataset.

How it works:
- The dataset dropdown is populated via `GET /v2/datasets` so you can pick by name.
- The connector calls `GET /v2/datasets/{datasetId}/items` with the provided `limit` and `offset` to fetch the data.
- To provide typed fields in Power Automate, it calls `GET /v2/datasets/{datasetId}/itemsSchemaHelper` to infer the item schema from a sample.

Tips:
- For large datasets, paginate using `limit` and `offset` to process items in batches.

#### Get Key-Value Store Record Action

Retrieve a record's content from a selected key-value store.

- **Authentication**: Use *Sign in with Apify* [OAuth 2.0] (scopes: `profile`, `full_api_access`).
- **Headers**: All requests include `x-apify-integration-platform: microsoft-power-automate`.
- **Inputs**:
  - `Store` (`storeId`, required): Dynamic dropdown listing your stores.
  - `Record Key` (`recordKey`, required): Dependent dropdown listing keys for the selected store.
- **Output**:
  - Body: Raw record content (handled as binary; text and JSON are shown accordingly by Power Automate).
  - Header: `Content-Type` is exposed as an output value.

This action calls `GET /v2/key-value-stores/{storeId}/records/{recordKey}` via Apify API proxy.

#### Scrape Single URL Action

Use the *Scrape single URL* action to scrape a single webpage using Apify's Web Scraper Actor.

- Authentication: Use *Sign in with Apify* [OAuth 2.0] (scopes: `profile`, `full_api_access`).
- Headers: All requests include `x-apify-integration-platform: microsoft-power-automate`.
- URL (`url`): The full URL of the single page to be scraped. Must be a valid URL format.
- Crawler Type (`crawler_type`): Select the crawling engine to use:
  - `playwright:adaptive` (Adaptive - recommended)
  - `playwright:firefox` (Firefox Headless Browser)
  - `cheerio` (Cheerio - Raw HTTP, fastest)

The connector invokes `POST /v2/acts/aYG0l9s7dbB7j3gbS/runs` (Web Scraper Actor). This action starts an asynchronous scrape and returns the run details immediately. Use the *Actor run finished* trigger to process results once the scrape is complete.

#### Run Actor Action

Use the *Run Actor* action to start an Apify Actor run.

- Authentication: Use *Sign in with Apify* [OAuth 2.0] (scopes: `profile`, `full_api_access`).
- Headers: All requests include `x-apify-integration-platform: microsoft-power-automate`.
- Actor Scope (`actorScope`): Choose *Recently used Actors* or *From store*.
  - If *Recently used Actors*: pick from `Actor` populated by your account Actors.
  - If *From store*: pick from `Actor` populated by Apify Store (limit 1000).
- Input Body (`inputBody`): Provide JSON for the Actor input.
- Optional query params:
  - `build`: specific build tag or id
  - `timeout` (seconds)
  - `memory` (MB): 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768
  - `waitForFinish` (seconds, max 60): set 0 to no limit

The connector invokes `POST /v2/acts/{actorId}/runs` per Apify docs (see: https://docs.apify.com/api/v2/act-runs-post). The `actorId` path segment is chosen automatically based on your `actorScope` selection.

#### Run Task Action

Use the *Run task* action to start an Apify task run.

- Authentication: Use *Sign in with Apify* [OAuth 2.0] (scopes: `profile`, `full_api_access`).
- Headers: All requests include `x-apify-integration-platform: microsoft-power-automate`.
- Task (`taskId`): Select the task from a dynamic dropdown of your available tasks.
- Input Body (`inputOverride`): (Optional) provide a raw JSON object to override the task's default input.
- Optional query params:
  - `timeout` (seconds)
  - `build`: specific build tag or id
  - `memory` (MB): 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768
  - `waitForFinish` (seconds, max 60). If empty or 0, the call is asynchronous (does not wait).

The connector invokes `POST /v2/actor-tasks/{taskId}/runs` per Apify docs (see: https://docs.apify.com/api/v2/actor-task-runs-post). The `taskId` path segment is selected directly from the dropdown.

## Getting Started

For detailed instructions on how to get started with the Apify Power Automate connector, please visit the [Apify Power Automate documentation](https://docs.apify.com/platform/integrations/power-automate).

## Known Issues and Limitations

- The *Wait for finish* parameter has a maximum of 60 seconds. For long-running Actors, use webhooks (triggers) instead of waiting
- Dataset schemas are inferred from sample data and may not capture all possible fields
- Dynamic schemas may not reflect all possible data structures in large or complex datasets
- Start with default memory settings and only increase if needed to optimize costs

## Frequently Asked Questions

**How much does it cost?**
The connector itself is free to use, but Apify charges for compute resources based on runtime, memory allocation, and proxy usage. Apify offers a free tier with monthly credits. For pricing details, visit [apify.com/pricing](https://apify.com/pricing).

**Where can I get help?**
- **Apify documentation**: [docs.apify.com](https://docs.apify.com)
- **API reference**: [docs.apify.com/api/v2](https://docs.apify.com/api/v2)
- **Apify store**: [apify.com/store](https://apify.com/store)
- **Support**: [apify.com/contact](https://apify.com/contact)
- **Community forum**: [community.apify.com](https://community.apify.com)

## Development Setup

### Prerequisites

* [Apify account](https://apify.com)
* [Power Automate environment](https://make.powerautomate.com/)
* [Python](https://www.python.org/downloads) 3.5 or later installed

### Clone Repository

```bash
git clone https://github.com/apify/apify-microsoft-power-automate-integration.git
cd apify-microsoft-power-automate-integration
```

### Install Python and Power Platform Connectors CLI

`paconn` requires Python and is installed via pip:

1. Verify Python installation:

   ```bash
   python --version
   ```

2. Install `paconn`:

   ```bash
   pip install paconn
   ```

> **Note:** Using a Python virtual environment is recommended to isolate dependencies.

### Verify Installation

```bash
paconn
```

You should see usage help output, confirming the CLI is installed.

### Authentication

Authenticate with your Power Platform environment using device code login:

```bash
paconn login
```

Follow the prompt to open [https://microsoft.com/devicelogin](https://microsoft.com/devicelogin) and enter the code shown in your terminal.

To logout:

```bash
paconn logout
```

### `settings.json` for paconn

You can use a settings.json file in this project root to store arguments for paconn commands. This simplifies repeated operations because the CLI reads environment, connector ID, file paths, and other parameters from this file when provided.

This makes subsequent commands shorter:

```bash
paconn create --settings settings.json
paconn update --settings settings.json
```

---

## Connector Files Explained

Apify's custom connector consists of the following core files:

* **apiDefinition.swagger.json** – The API description in OpenAPI/Swagger format, listing endpoints, inputs, and outputs that determine what actions and triggers appear in Power Automate.
* **apiProperties.json** – Connector metadata such as display name, authentication settings, host, and other configuration details.
* **scripts.csx** – A C# script for custom request/response logic not covered by the API definition.
* **icon.png** – The image shown as the connector’s icon in the Power Automate UI.

These definitions are stored locally and pushed to the Power Platform environment with paconn commands.

---

## Creating and Updating the Connector

### When to Create

If the connector does not yet exist in your Power Automate environment, create it once:

```bash
paconn create --settings settings.json --secret <oauth-client-secret>
```

or explicitly without settings:

```bash
paconn create -e <ENV_ID> --api-prop apiProperties.json --api-def apiDefinition.swagger.json --icon icon.png --script scripts.csx --secret <oauth-client-secret>
```

After creation, paconn prints the `connector ID`. Save this value in your `settings.json` for future updates.

### When to Update

Once the connector is created and you are modifying its definition locally, use the update command:

```bash
paconn update --settings settings.json --secret <oauth-client-secret>
```

or explicitly:

```bash
paconn update -e <ENV_ID> -c <CONNECTOR_ID> --api-prop apiProperties.json --api-def apiDefinition.swagger.json --icon icon.png --script scripts.csx --secret <oauth-client-secret>
```

---

## Development Cycle

1. **Edit Locally**
   Update `apiDefinition.swagger.json`, `apiProperties.json`, and `scripts.csx` in your IDE.

2. **Deploy Updates**
   Push changes:

   ```bash
   paconn update --settings settings.json --secret <oauth-client-secret>
   ```
3. **Check for Errors**
   Go to custom connector edit mode in Power Automate and try saving the connector. If there are errors, check the error message, fix them localy and repeat.
4. **Test Changes**
   Run flows using your connector’s actions and triggers to verify behavior.
5. **Repeat**
   Fix issues locally, then update and test again.

---

## CI/CD Integration

The repository includes GitHub Actions workflows:

* **Validation**: Validates connector files on pull requests
* **Deployment**: There is no deployment workflow yet, due to limitations of the Power Platform Connectors CLI.

---

## Resources

* [Apify API Documentation](https://docs.apify.com/api/v2)
* [Microsoft Power Automate Documentation](https://docs.microsoft.com/en-us/power-automate/)
* [Power Platform Connectors Documentation](https://docs.microsoft.com/en-us/connectors/custom-connectors/)
* [Power Platform Connectors CLI Documentation](https://learn.microsoft.com/en-us/connectors/custom-connectors/paconn-cli)

---

Maintained by: Apify Team
Support: [GitHub Issues](https://github.com/apify/apify-microsoft-power-automate-integration/issues)

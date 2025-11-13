# Apify Connector

Apify is a web scraping and automation platform that provides serverless computing infrastructure for running data extraction tasks called Actors. This connector enables Microsoft Power Automate users to integrate Apify's web scraping capabilities directly into their workflows. Run Actors and Tasks, fetch data from Datasets and Key-Value Stores, and create webhooks to trigger flows when scraping jobs complete.

## Prerequisites

Before using this connector, you need to set up the following:

- An Apify account. Sign up at the [Apify Console](https://console.apify.com/).

## Supported Operations

This connector provides the following triggers and actions:

### Triggers

#### Actor Run Finished Trigger

Use the "Actor Run Finished" trigger to automatically execute your Power Automate flow when a specific Apify Actor run completes with a selected status.

- **Authentication**: Use Apify API token or Sign in with Apify [OAuth 2.0] (scopes: `profile`, `full_api_access`).
- **Headers**: All requests include `x-apify-integration-platform: microsoft-power-automate`.
- **Inputs**:
  - `Actor Scope`: Choose between "Recently used Actors" or "From Store" (Apify Store actors).
  - `Actor`: Dynamic dropdown populated with actors based on the selected scope.
  - `Trigger On`: Select which run statuses should trigger the flow (SUCCEEDED, FAILED, TIMED_OUT, ABORTED).
- **Output**: Webhook payload containing detailed information about the completed actor run.

How it works:
- Actor dropdown is populated via `GET /v2/acts` (for recent actors) or via `GET /v2/store` store API (for store actors).
- The trigger creates a webhook via `POST /v2/webhooks` that subscribes to actor run events.
- When the selected actor finishes with one of the specified statuses, Apify sends a webhook payload to Power Automate.

#### Actor Task Finished Trigger

Use the "Actor Task Finished" trigger to automatically execute your Power Automate flow when a specific Apify Actor task run completes with a selected status.

- **Authentication**: Use Apify API token or Sign in with Apify [OAuth 2.0] (scopes: `profile`, `full_api_access`).
- **Headers**: All requests include `x-apify-integration-platform: microsoft-power-automate`.
- **Inputs**:
  - `Task`: Dynamic dropdown populated with your actor tasks.
  - `Trigger On`: Select which run statuses should trigger the flow (SUCCEEDED, FAILED, TIMED_OUT, ABORTED).
- **Output**: Webhook payload containing detailed information about the completed task run.

How it works:
- Creates a webhook via `POST /v2/webhooks` that subscribes to actor task run events.
- Task dropdown is populated via `GET /v2/actor-tasks` to list your available tasks.

### Actions

#### Get Dataset Items Action

Use the "Get Dataset Items" action to retrieve records from one of your Apify Datasets.

- Authentication: Use Apify API token or Sign in with Apify [OAuth 2.0] (scopes: `profile`, `full_api_access`).
- Headers: All requests include `x-apify-integration-platform: power-automate`.
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

- **Authentication**: Apify API token or Sign in with Apify [OAuth 2.0] (scopes: `profile`, `full_api_access`).
- **Headers**: All requests include `x-apify-integration-platform: power-automate`.
- **Inputs**:
  - `Store` (`storeId`, required): Dynamic dropdown listing your stores.
  - `Record Key` (`recordKey`, required): Dependent dropdown listing keys for the selected store.
  - `Format Hint` (`format`, optional): `auto` (default), `json`, `text`, `binary`.
- **Output**:
  - Body: Raw record content (handled as binary; text and JSON are shown accordingly by Power Automate).
  - Header: `Content-Type` is exposed as an output value.

This action calls `GET /v2/key-value-stores/{storeId}/records/{recordKey}` via Apify API proxy.

#### Scrape Single URL Action

Use the "Scrape Single URL" action to scrape a single webpage using Apify's Web Scraper actor.

- Authentication: Use Apify API token or Sign in with Apify [OAuth 2.0] (scopes: `profile`, `full_api_access`).
- Headers: All requests include `x-apify-integration-platform: power-automate`.
- URL (`url`): The full URL of the single page to be scraped. Must be a valid URL format.
- Crawler Type (`crawler_type`): Select the crawling engine to use:
  - `playwright:adaptive` (Adaptive - recommended)
  - `playwright:firefox` (Firefox Headless Browser)
  - `cheerio` (Cheerio - Raw HTTP, fastest)
  - `jsdom` (JSDOM - Raw HTTP with JS support)
  - `playwright:chrome` (Chrome Headless Browser - deprecated)

The connector invokes `POST /v2/acts/aYG0l9s7dbB7j3gbS/runs` (Web Scraper actor) per Apify docs. This action starts an asynchronous scrape and returns the run details immediately. Use the Actor Run Finished trigger to process results once the scrape is complete.

#### Run Actor Action

Use the "Run Actor" action to start an Apify Actor run.

- Authentication: Use Apify API token or Sign in with Apify [OAuth 2.0] (scopes: `profile`, `full_api_access`).
- Headers: All requests include `x-apify-integration-platform: microsoft-power-automate`.
- Actor Scope (`actorScope`): Choose "Recently used Actors" or "From Store".
  - If "Recently used Actors": pick from `Actor` populated by your account Actors.
  - If "From Store": pick from `Actor` populated by Apify Store (limit 1000).
- Input Body (`inputBody`): Provide JSON for the Actor input.
- Optional query params:
  - `build`: specific build tag or id
  - `timeout` (seconds)
  - `memory` (MB): 512, 1024, 2048, 4096, 8192, 16384
  - `waitForFinish` (seconds, max 60): set 0 to no limit

The connector invokes `POST /v2/acts/{actorId}/runs` per Apify docs (see: https://docs.apify.com/api/v2/act-runs-post). The `actorId` path segment is chosen automatically based on your `actorScope` selection.

#### Run Task Action

Use the "Run Task" action to start an Apify task run.

- Authentication: Use Apify API token or Sign in with Apify [OAuth 2.0] (scopes: `profile`, `full_api_access`).
- Headers: All requests include `x-apify-integration-platform: microsoft-power-automate`.
- Task (`taskId`): Select the task from a dynamic dropdown of your available tasks.
- Input Body (`inputOverride`): Provide a raw JSON object to override the task's default input.
- Optional query params:
  - `timeout` (seconds)
  - `memory` (MB): 512, 1024, 2048, 4096, 8192, 16384
  - `waitForFinish` (seconds, max 60). If empty or 0, the call is asynchronous (does not wait).

The connector invokes `POST /v2/actor-tasks/{taskId}/runs` per Apify docs (see: https://docs.apify.com/api/v2/actor-task-runs-post). The `taskId` path segment is selected directly from the dropdown.

## Obtaining Credentials

The connector supports only OAuth 2.0 authentication with the following scopes:
- `profile`: Access to your profile information
- `full_api_access`: Full access to the Apify API

When creating a connection in Power Automate, select OAuth 2.0. You will be redirected to Apify to authorize access to your account.

**Note**: All requests include the header `x-apify-integration-platform: microsoft-power-automate` to identify the integration platform.

## Getting Started

For detailed instructions on how to get started with the Apify Power Automate connector, please visit the [Apify Power Automate documentation](https://docs.apify.com/platform/integrations/power-automate).

## Known Issues and Limitations

- The "Wait for Finish" parameter has a maximum of 60 seconds. For long-running Actors, use webhooks (triggers) instead of waiting
- Dataset schemas are inferred from sample data and may not capture all possible fields
- Dynamic schemas may not reflect all possible data structures in large or complex datasets
- Start with default memory settings and only increase if needed to optimize costs

## Frequently Asked Questions

**How much does it cost?**
The connector itself is free to use, but Apify charges for compute resources based on runtime, memory allocation, and proxy usage. Apify offers a free tier with monthly credits. For pricing details, visit [apify.com/pricing](https://apify.com/pricing).

**Where can I get help?**
- **Apify Documentation**: [docs.apify.com](https://docs.apify.com)
- **API Reference**: [docs.apify.com/api/v2](https://docs.apify.com/api/v2)
- **Apify Store**: [apify.com/store](https://apify.com/store)
- **Support**: [apify.com/contact](https://apify.com/contact)
- **Community Forum**: [community.apify.com](https://community.apify.com)

## Development Setup

### Clone Repository

```bash
git clone https://github.com/apify/apify-microsoft-power-automate-integration.git
cd apify-microsoft-power-automate-integration
```

### Install .NET SDK

The Power Platform CLI requires .NET SDK to be installed on your system. 

1. Download and install the latest .NET SDK from the [official .NET download page](https://dotnet.microsoft.com/en-us/download/dotnet).
   - For Windows, Linux, or macOS, select the appropriate installer for your operating system
   - The recommended version is .NET 8.0 (LTS) or later
   - After installation, verify by running `dotnet --version` in your terminal

### Install Power Platform CLI

The Power Platform CLI (pac) is required for development and deployment of the connector. There are three ways to install it:

1. **Visual Studio Code Extension** (Windows, Linux, macOS):
   - Install the [Power Platform Tools extension](https://marketplace.visualstudio.com/items?itemName=microsoft-IsvExpTools.powerplatform-vscode)
   - This makes pac CLI available within VS Code terminals

2. **.NET Tool** (Windows, Linux, macOS):
   ```bash
   dotnet tool install -g Microsoft.PowerApps.CLI.Tool
   ```
   - Requires .NET SDK to be installed first

3. **Windows MSI** (Windows only):
   - Download and install from [Microsoft Download Center](https://aka.ms/PowerAppsCLI)
   - Enables all commands on Windows

### Verify Installation

To verify the installation:

```bash
# Check if pac is installed and the version (second row)
pac
```

### Authentication Setup

Authenticate with the Power Platform CLI to create a saved profile that stores credentials and selects the environment for subsequent `pac` commands:

```bash
# Create a profile for a specific environment (use --deviceCode for CLI auth)
pac auth create --environment "<ENV_ID-or-URL>"

# Manage profiles
pac auth list
pac auth select --profile "<profileName>"
```

- **Important:** Omitting `--environment` can leave no active environment set and cause later `pac` commands to fail.
- **Find ENV_ID:** copy it from the Maker portal URL, e.g. `https://make.powerapps.com/environments/<ENV_ID>/...` (the `<ENV_ID>` segment is your Environment ID).

Verify connectivity with `pac connector list`.

### Using Settings File

The repository includes a `settings.json` file that simplifies connector operations by storing configuration parameters. This eliminates the need to specify all parameters in each command.

1. **Update the settings file:**
   
   Before using the settings file, make sure to update the following fields.
   
   ```json
   {
     "connectorId": "YOUR-CONNECTOR-ID",
     "environment": "YOUR-ENVIRONMENT-ID",
     "apiProperties": "apiProperties.json",
     "apiDefinition": "apiDefinition.swagger.json",
     "icon": "icon.png",
     "script": "scripts.csx"
   }
   ```
   
   - Replace `YOUR-CONNECTOR-ID` with your actual connector ID (if you already have one, otherwise leave it be)
   - Replace `YOUR-ENVIRONMENT-ID` with your Power Platform environment ID

### Initial Setup

Before you start development, you need to either create a new connector or download an existing one:

#### Create a New Connector

If you don't have an Apify connector in your Power Automate environment yet:

```bash
pac connector create --settings-file settings.json --solution-unique-name <your_solution_unique_name>
```

After creation, list your connectors to get the ID for future operations:

```bash
pac connector list
```

Find your new Apify connector in the list and note its `ConnectorId`.

### Download an Existing Connector

If you already have an Apify connector in your environment and want to work on it:

1. First, list available connectors to find the ID:

   ```bash
   pac connector list
   ```

2. Download the connector files to your local environment:

   ```bash
   pac connector download \
     --connector-id <connector-id> \
     --outputDirectory ./
   ```

## Deployment Instructions

### Creating a New Connector

To deploy this connector as a custom connector in Power Automate:

1. Ensure you have completed the [Prerequisites](#prerequisites) and [Development Setup](#development-setup)
2. Update the `settings.json` file with your environment ID
3. Create the connector:

   ```bash
   pac connector create --settings-file settings.json --solution-unique-name <your_solution_unique_name>
   ```

4. After creation, note the `ConnectorId` from `pac connector list`

### Updating an Existing Connector

To update an existing connector:

1. Make your changes to the connector files locally
2. Update the connector:

   ```bash
   pac connector update --settings-file settings.json
   ```

### CI/CD

The repository includes GitHub Actions workflows for:

1. **Validation**: Validates the connector files on every pull request
2. **Deployment**: Deploys the connector to the development environment on merge to main branch

#### Workflow Files

- `.github/workflows/validate.yml`: Validates the connector files
- `.github/workflows/deploy.yml`: Deploys the connector to the development environment

## Development Cycle

Once you have your connector set up and uploaded, follow this development cycle:

1. **Edit Locally**

   Make changes to the connector files in your local IDE:
   - `apiDefinition.swagger.json` - OpenAPI definition
   - `apiProperties.json` - Connector properties
   - `scripts.csx` - Custom scripts

2. **Update the Connector**

   Push your changes to Power Automate:

   ```bash
   pac connector update --settings-file settings.json
   ```

3. **Test Your Changes**

   Before testing, you need a valid connection:
   - Go to `Connections -> New connection` and create a connection to your connector
   - Ensure the connection shows as `Connected`
   - Alternatively, create the connection directly in the `Test tab`

   Test your connector in Power Automate:
   - Navigate to `Custom connectors -> Apify -> Test tab`
   - Try different operations to verify your changes
   - You can also use the `Swagger editor` for testing and fine-tuning

   Create test flows in Power Automate to validate each action and trigger:
   - Verify authentication works correctly
   - Test with various input parameters and edge cases
   - Validate output schema matches expectations

4. **Iterate**

   - If you find issues, return to your local IDE
   - Make necessary fixes to the files
   - Update the connector again using the `pac connector update` command
   - Repeat the testing process


## Resources

- [Apify API Documentation](https://docs.apify.com/api/v2)
- [Microsoft Power Automate Documentation](https://docs.microsoft.com/en-us/power-automate/)
- [Power Platform Connectors Documentation](https://docs.microsoft.com/en-us/connectors/custom-connectors/)
- [Power Platform CLI Documentation](https://learn.microsoft.com/en-us/power-platform/developer/cli/introduction)
- [Custom Connector OpenAPI Definition](https://learn.microsoft.com/en-us/connectors/custom-connectors/define-openapi-definition)

---

Maintained by: Apify Team

Support: [GitHub Issues](https://github.com/apify/apify-microsoft-power-automate-integration/issues)

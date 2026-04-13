# Apify

Apify is a full-stack web scraping and automation platform. The Apify connector for Power Automate lets you run web scrapers and AI agents (called Actors), execute pre-configured tasks, retrieve structured data from datasets and key-value stores, and trigger automated workflows when jobs finish. Over 4,000 ready-made Actors are available on the Apify Store for common scraping and automation use cases.

## Publisher

Apify Technologies s.r.o.

## Prerequisites

- **An Apify account** — sign up for free at [apify.com](https://apify.com). A free tier with monthly platform credits is included.
- **OAuth 2.0 authorization** — the connector authenticates through Apify's OAuth flow. No API tokens or manual credential setup is needed from end users.

## How to get credentials

The connector uses OAuth 2.0 authentication. When you add the Apify connector to a flow and create a new connection, you are automatically redirected to the Apify Console to authorize access. Follow these steps:

1. In your Power Automate flow, add any Apify action or trigger.
2. When prompted to create a connection, click **Sign in**.
3. You will be redirected to the **Apify Console** authorization page at `console.apify.com`.
4. If you are not logged in to Apify, enter your Apify account email and password (or use Google/GitHub sign-in).
5. Review the requested permissions:
   - **profile** — access to your user profile information
   - **full_api_access** — full access to the Apify API on your behalf
6. Click **Authorize** to grant the connector access.
7. You will be redirected back to Power Automate with an active connection.

No manual entry of API tokens, client IDs, or secrets is required from end users. The connector handles token refresh automatically.

### For the Microsoft certification review team

To test the connector, you will need an Apify account. You can create one for free at [apify.com](https://apify.com) — no credit card is required. The free tier includes enough platform credits to run test Actors and verify all connector operations. After creating an account, follow steps 1–7 above to authorize the connector.

## Supported operations

### Actions

| Operation | Description |
|-----------|-------------|
| **Run Actor** | Runs an Apify Actor with the specified input. You can select from recently used Actors or browse the Apify Store. Optionally wait up to 60 seconds for the run to finish. Returns run metadata including status, dataset ID, and key-value store ID. |
| **Run task** | Runs a pre-configured Actor task. Tasks store default input configurations so you can execute them without specifying input each time. Optionally override the default input with custom values. |
| **Scrape single URL** | Scrapes a single web page using the Apify Web Scraper Actor. Choose between adaptive (recommended), Firefox headless browser, or Cheerio (raw HTTP) crawling modes. Returns the run metadata for tracking. |
| **Get dataset items** | Retrieves items from an Apify dataset. Datasets store structured results from Actor runs. Supports pagination with limit and offset parameters. The response schema is dynamically inferred from the dataset contents. |
| **Get key-value store record** | Retrieves a record from an Apify key-value store by its key. Key-value stores hold files, configurations, and run state. The response schema is dynamically inferred from the record contents. |

### Triggers

| Operation | Description |
|-----------|-------------|
| **Actor run finished** | Fires when a selected Actor run finishes. Subscribe to specific completion statuses: succeeded, failed, timed out, or aborted. All statuses are enabled by default. The trigger payload includes full run details and metadata. |
| **Actor task finished** | Fires when a selected Actor task run finishes. Same status filtering options as the Actor run finished trigger. Useful for monitoring scheduled or recurring task executions. |

## Getting started

### Basic workflow: Run an Actor and get results

1. Add the **Run Actor** action to your flow.
2. Select an Actor from your recently used Actors or from the Apify Store.
3. Provide the input JSON required by the Actor.
4. Add the **Get dataset items** action.
5. Use the **Default dataset ID** output from the Run Actor step as the Dataset parameter.
6. Process the returned items in subsequent flow steps.

### Event-driven workflow: React to completed runs

1. Add the **Actor run finished** trigger as the flow's trigger.
2. Select the Actor you want to monitor.
3. Choose which completion statuses should trigger the flow (all enabled by default).
4. In subsequent steps, use the trigger payload to access run details and retrieve results.

### Quick scrape workflow

1. Add the **Scrape single URL** action.
2. Enter the URL you want to scrape.
3. Select a crawler type (Adaptive is recommended for most sites).
4. Use the **Actor run finished** trigger or poll for completion to get results.

## Known issues and limitations

- The **Wait for Finish** parameter has a maximum of 60 seconds. For long-running Actors, use the **Actor run finished** trigger instead of waiting.
- Dataset schemas are dynamically inferred from a sample item. They may not capture all possible fields if the dataset contains items with varying structures.
- Key-value store record schemas are inferred from the record content. Binary records (images, PDFs) are returned as-is without schema inference.
- The **Scrape single URL** action uses a fixed Actor (Web Scraper). For advanced scraping scenarios, use **Run Actor** with a specialized Actor from the Apify Store.
- Memory allocation options range from 128 MB to 32 GB. Start with default settings to optimize costs.

## Common errors and remedies

| Error | Remedy |
|-------|--------|
| **Unable to authenticate** | Ensure you have an active Apify account. Try removing the connection and creating a new one. |
| **Actor run returns FAILED status** | Check the Actor run logs in the Apify Console for detailed error messages. Verify the input matches the Actor's expected format. |
| **Dataset is empty after run completes** | Confirm the Actor run finished with SUCCEEDED status. The target website's structure may have changed. |
| **Flow times out waiting for Actor** | Set "Wait for Finish" to 0 and use the "Actor run finished" trigger to handle long-running Actors asynchronously. |
| **Webhook trigger not firing** | Verify the Actor or task ID is correct. Check that at least one event status is enabled. |

## FAQ

**How much does Apify cost?**
The connector itself is free. Apify charges for platform usage based on compute time and resources consumed. A free tier with monthly credits is included with every account. See [apify.com/pricing](https://apify.com/pricing) for details.

**What are Actors?**
Actors are serverless cloud programs that run on the Apify platform. They can perform web scraping, data extraction, AI automation, and more. Browse over 4,000 ready-made Actors at [apify.com/store](https://apify.com/store).

**What are tasks?**
Tasks are saved configurations for Actors. They store default input values so you can run an Actor with pre-set parameters without re-entering them each time.

## Helpful links

- [Apify documentation](https://docs.apify.com)
- [Apify API reference](https://docs.apify.com/api/v2)
- [Apify Store — browse Actors](https://apify.com/store)
- [Apify pricing](https://apify.com/pricing)
- [Support and contact](https://apify.com/contact)
- [Community forum](https://community.apify.com)

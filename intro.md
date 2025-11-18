# Apify Connector

The Apify connector enables you to automate web scraping and data extraction workflows directly in your flows. With this connector, you can run *Actors* and *tasks*, retrieve scraped data from *datasets* and *key-value stores*, and create *webhooks* to trigger workflows when scraping jobs finish.

## Prerequisites

- An Apify account - Sign up for free at [apify.com](https://apify.com)
- Sign in with Apify authentication [OAuth 2.0] - The connector uses Sign in with Apify for secure authentication

## How to get credentials

The connector is pre-configured with Sign in with Apify settings, including a shared client ID and client secret that all users can use. When you create a connection, you will be redirected to the Apify Console to authorize the connection. No additional credential setup is required on your part.

The connector requires the following OAuth scopes:
- `profile` - Access to your user profile
- `full_api_access` - Full access to the Apify API

## Get started with your connector

1. Create a connection to Apify using Sign in with Apify authentication [OAuth 2.0]
2. Select an action from the available list:
   - **Run Actor** - Start an Actor run for web scraping
   - **Run Task** - Execute a pre-configured Actor Task
   - **Scrape Single URL** - Quickly scrape a single webpage
   - **Get Dataset Items** - Retrieve scraped data from a Dataset
   - **Get Key-Value Store Record** - Access files and metadata from a Key-Value Store
3. Use triggers to automate workflows:
   - **Actor Run Finished** - Trigger when an Actor run completes
   - **Actor Task Finished** - Trigger when a Task completes

## Known issues and limitations

- The "Wait for Finish" parameter has a maximum of 60 seconds. For long-running Actors, use webhooks (triggers) instead of waiting
- Dataset schemas are inferred from sample data and may not capture all possible fields
- Dynamic schemas may not reflect all possible data structures in large or complex datasets
- Start with default memory settings and only increase if needed to optimize costs

## Common errors and remedies

**Unable to authenticate**
- Make sure you have an active Apify account and try reconnecting

**Actor returns FAILED status**
- Check the Actor run logs in Apify Console for detailed error messages
- Verify your input configuration matches the Actor's expected format

**Dataset is empty after Actor completes**
- Check if the Actor ran successfully (status = SUCCEEDED)
- Verify the website structure hasn't changed (scraping targets may have been updated)

**Workflow times out waiting for Actor**
- Set "Wait for Finish" to 0 and use the "Actor Run Finished" trigger instead
- Increase the Actor's timeout setting if it's legitimately taking longer

## FAQ

**How much does it cost?**
The connector itself is free to use, but Apify charges for compute resources based on runtime, memory allocation, and proxy usage. Apify offers a free tier with monthly credits. For pricing details, visit [apify.com/pricing](https://apify.com/pricing).

**Where can I get help?**
- **Apify Documentation**: [docs.apify.com](https://docs.apify.com)
- **API Reference**: [docs.apify.com/api/v2](https://docs.apify.com/api/v2)
- **Apify Store**: [apify.com/store](https://apify.com/store)
- **Support**: [apify.com/contact](https://apify.com/contact)
- **Community Forum**: [community.apify.com](https://community.apify.com)

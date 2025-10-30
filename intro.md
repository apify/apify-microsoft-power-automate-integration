# Apify Connector

## Overview

The Apify connector enables you to automate web scraping and data extraction workflows directly in your flows. With this connector, you can run Actors and Tasks, retrieve scraped data from Datasets and Key-Value Stores, and create webhooks to trigger workflows when scraping jobs finish.

## What is Apify?

Apify is a web scraping and automation platform that provides serverless infrastructure for running web scrapers and data extraction tools called Actors. The platform handles all the complexity of running browsers, managing proxies, and scaling compute resources, so you can focus on extracting the data you need.

## Prerequisites

To use this connector, you need:

1. **An Apify account** - Sign up for free at [apify.com](https://apify.com)
2. **Authentication** - The connector supports OAuth 2.0 authentication

### Getting Started with Apify

If you're new to Apify:
1. Create a free account at [apify.com](https://apify.com)
2. Browse the [Apify Store](https://apify.com/store) to find ready-made Actors for popular websites
3. Try running an Actor from the Apify Console to understand how it works

## Authentication

When you create a connection to Apify, you'll be prompted to authenticate using OAuth 2.0. This will redirect you to the Apify Console where you can authorize the connection. The connector requires the following permissions:
- `profile` - Access to your user profile
- `full_api_access` - Full access to the Apify API

Once authenticated, the connector will have access to run your Actors, retrieve data, and manage webhooks on your behalf.

## Available Actions

### Run Actor

Starts an Actor run and returns immediately without waiting for completion. Use this action when you want to trigger a scraping job and process the results later.

**When to use:**
- Starting long-running scraping jobs
- Triggering multiple Actors in parallel
- When you'll use webhooks to get notified of completion

**Configuration:**
- **Actor Scope**: Choose between "Recently used Actors" or "From Store"
- **Actor**: Select the Actor you want to run
- **Input Body**: Provide JSON configuration for the Actor
- **Build** (optional): Specify a particular build version
- **Timeout** (optional): Maximum runtime in seconds
- **Memory** (optional): Memory allocation in MB (128, 256, 512, 1024, 2048, 4096, 8192, or 16384)
- **Wait for Finish** (optional): Maximum seconds to wait for completion (0-60)

**Tips:**
- Keep "Wait for Finish" at 0 for long-running jobs to avoid timeouts
- Use the Actor's documentation to understand what input it expects
- Start with the default memory setting and increase only if needed

### Run Task

Runs a pre-configured Actor Task. Tasks are saved configurations of Actors with predefined inputs, making them easier to reuse.

**When to use:**
- Running the same scraping job repeatedly with consistent settings
- When you've already configured and tested the Actor settings in Apify Console

**Configuration:**
- **Task**: Select from your saved Actor Tasks
- **Input Override** (optional): JSON to override the task's default input
- **Timeout** (optional): Maximum runtime in seconds
- **Memory** (optional): Memory allocation in MB
- **Wait for Finish** (optional): Maximum seconds to wait (0-60)

### Scrape Single URL

A convenient action for quickly scraping a single webpage without configuring a full Actor.

**When to use:**
- Quick one-off scraping of a single page
- Extracting data from a specific URL
- Testing before setting up more complex scraping workflows

**Configuration:**
- **URL**: The full URL of the page to scrape
- **Crawler Type**: Choose the scraping engine:
  - **Adaptive** (recommended) - Automatically selects the best method
  - **Firefox** - Full browser rendering with Firefox
  - **Cheerio** - Fast HTTP-only scraping (no JavaScript)
  - **JSDOM** - HTTP with basic JavaScript support
  - **Chrome** - Full browser rendering with Chrome (deprecated)

**Tips:**
- Use "Adaptive" for most websites
- Use "Cheerio" for static HTML pages (fastest)
- Use Firefox or Chrome for JavaScript-heavy websites

### Get Dataset Items

Retrieves data from an Apify Dataset. Datasets contain the results from Actor runs, with each item being a row of extracted data.

**When to use:**
- Retrieving scraped data after an Actor finishes
- Processing results from completed scraping jobs
- Paginating through large datasets

**Configuration:**
- **Dataset**: Select from your datasets
- **Limit** (optional): Number of items to retrieve
- **Offset** (optional): Number of items to skip (for pagination)

**Tips:**
- Use "Apply to each" loops to process individual items
- For large datasets, use pagination with limit and offset
- The schema adapts to match your dataset's structure

### Get Key-Value Store Record

Retrieves a specific record from a Key-Value Store. Actors often save output files, screenshots, or metadata in Key-Value Stores.

**When to use:**
- Retrieving screenshots saved by an Actor
- Getting HTML snapshots or log files
- Accessing metadata or summary results

**Configuration:**
- **Store**: Select the Key-Value Store
- **Record Key**: Select the key of the record to retrieve

**Common keys:**
- `OUTPUT` - Main output file (often JSON)
- `INPUT` - The input configuration used
- Screenshots are usually named `screenshot-1.png`, `screenshot-2.png`, etc.

## Available Triggers

### Actor Run Finished

Triggers your workflow when a selected Actor run completes with a specific status.

**When to use:**
- Processing scraped data immediately after an Actor finishes
- Getting notifications about failed scraping jobs
- Building end-to-end automation workflows

**Configuration:**
- **Actor Scope**: Recently used or from Store
- **Actor**: Select which Actor to monitor
- **Trigger On**: Choose which statuses trigger the workflow:
  - **SUCCEEDED** - Actor completed successfully
  - **FAILED** - Actor failed with an error
  - **TIMED_OUT** - Actor exceeded the timeout limit
  - **ABORTED** - Actor was manually stopped

**Output:**
The trigger provides detailed information about the run, including:
- Run ID, status, and timing information
- Default Dataset ID (where results are stored)
- Exit code and status messages
- Pricing and usage information

**Tips:**
- Use the Dataset ID from the trigger output to retrieve results
- Check the status field to handle different outcomes
- The trigger includes the full Actor run details in the payload

### Actor Task Finished

Similar to Actor Run Finished, but monitors a specific Task instead of an Actor.

**When to use:**
- Monitoring scheduled or recurring scraping tasks
- Building workflows around pre-configured Tasks
- Processing results from specific Task configurations

**Configuration:**
- **Task**: Select which Task to monitor
- **Trigger On**: Choose which statuses trigger the workflow (SUCCEEDED, FAILED, TIMED_OUT, ABORTED)

## Common Workflows

### Scrape Website and Process Results

1. Use "Run Actor" action to start a scraping job
2. Use "Actor Run Finished" trigger to detect completion
3. Use "Get Dataset Items" action to retrieve the scraped data
4. Process the data with other connectors (Excel, SharePoint, SQL, etc.)

### Scheduled Scraping with Notifications

1. Use a recurrence trigger to run on a schedule
2. Use "Run Task" action to start your pre-configured scraping task
3. Use "Actor Run Finished" trigger to wait for completion
4. Use Office 365 Outlook connector to email the results

### Monitor Price Changes

1. Use "Scrape Single URL" for product pages
2. Parse the price from the results
3. Store in a database or spreadsheet
4. Use conditions to send alerts when prices drop

## Limitations and Best Practices

### Known Limitations

- The "Wait for Finish" parameter has a maximum of 60 seconds
- For long-running Actors, use webhooks (triggers) instead of waiting
- Dataset schemas are inferred from sample data and may not capture all possible fields

### Best Practices

1. **Start Small**: Test your Actors in the Apify Console before building workflows
2. **Handle Errors**: Use "Actor Run Finished" trigger with FAILED status to handle errors
3. **Optimize Memory**: Start with default memory and only increase if you see out-of-memory errors
4. **Use Tasks**: For recurring jobs, create Tasks in Apify Console for easier management
5. **Pagination**: When working with large datasets, use limit and offset for pagination
6. **Check Credits**: Monitor your Apify account usage to avoid unexpected charges

## Pricing

The connector itself is free to use, but Apify charges for compute resources based on:
- Runtime (compute units per second)
- Memory allocated
- Proxy usage (if enabled)

Apify offers a free tier with monthly credits. For pricing details, visit [apify.com/pricing](https://apify.com/pricing).

## Support and Resources

- **Apify Documentation**: [docs.apify.com](https://docs.apify.com)
- **API Reference**: [docs.apify.com/api/v2](https://docs.apify.com/api/v2)
- **Apify Store**: [apify.com/store](https://apify.com/store)
- **Support**: [apify.com/contact](https://apify.com/contact)
- **Community Forum**: [community.apify.com](https://community.apify.com)

## Example Use Cases

- **E-commerce**: Monitor competitor prices and product availability
- **Real Estate**: Track property listings and price changes
- **Social Media**: Collect public posts and engagement metrics
- **News Monitoring**: Aggregate articles from multiple sources
- **Market Research**: Gather business information and reviews
- **Job Boards**: Collect job postings matching specific criteria

## Troubleshooting

### Connection Issues

**Problem**: Unable to authenticate
- **Solution**: Make sure you have an active Apify account and try reconnecting

### Actor Runs Failing

**Problem**: Actor returns FAILED status
- **Solution**: Check the Actor run logs in Apify Console for detailed error messages
- **Solution**: Verify your input configuration matches the Actor's expected format

### Missing Data

**Problem**: Dataset is empty after Actor completes
- **Solution**: Check if the Actor ran successfully (status = SUCCEEDED)
- **Solution**: Verify the website structure hasn't changed (scraping targets may have been updated)

### Timeout Errors

**Problem**: Workflow times out waiting for Actor
- **Solution**: Set "Wait for Finish" to 0 and use the "Actor Run Finished" trigger instead
- **Solution**: Increase the Actor's timeout setting if it's legitimately taking longer

## Getting Help

If you encounter issues:
1. Check the Actor's documentation in the Apify Store
2. Review the run details in Apify Console for error messages
3. Contact Apify support at [apify.com/contact](https://apify.com/contact)
4. Visit the Apify community forum for community support

---

**Version**: 1.0.0  
**Publisher**: Apify  
**License**: Apache 2.0


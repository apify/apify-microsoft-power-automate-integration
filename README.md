# Apify Microsoft Power Automate Integration

Connect your Microsoft Power Automate workflows with Apify's web scraping platform. Run Actors and Tasks, fetch data from Datasets and Key-Value Stores, and create Apify webhooks.

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Development Setup](#development-setup)
- [Connector Structure](#connector-structure)
- [Development Workflow](#development-workflow)
- [Deployment](#deployment)
- [Testing](#testing)
- [CI/CD](#cicd)
- [Troubleshooting](#troubleshooting)
- [Resources](#resources)

## Overview

This connector enables Microsoft Power Automate users to use Apify's web scraping capabilities directly in their workflows. Apify provides serverless computing infrastructure for running web scraping, data extraction tasks called Actors.

## Prerequisites

### Clone Repository

```bash
git clone https://github.com/apify/apify-microsoft-power-automate-integration.git
cd apify-microsoft-power-automate-integration
```

## Connector Structure

```
.
├── apiDefinition.swagger.json  # OpenAPI definition of the connector
├── apiProperties.json          # Connector properties and metadata
├── scripts.csx                 # Custom connector scripts
├── icon.png                    # Connector icon
├── settings.json               # Configuration for connector commands
├── .github/
│   └── workflows/              # CI/CD pipeline configurations
├── .gitignore                  # Git ignore file
└── README.md                   # Documentation
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

## Development Setup

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

## Development Workflow

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

#### Download an Existing Connector

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

### Development Cycle

Once you have your connector set up, follow this development cycle:

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

4. **Iterate**

   - If you find issues, return to your local IDE
   - Make necessary fixes to the files
   - Update the connector again using the `pac connector update` command
   - Repeat the testing process

## Actions

### Run Actor Action

Use the "Run Actor" action to start an Apify Actor run.

- Authentication: Use Apify API Key or OAuth 2.0 (scopes: `profile`, `full_api_access`).
- Headers: All requests include `x-apify-integration-platform: power-automate`.
- Actor Source (`actor_scope`): Choose "My Actors" or "From Store".
  - If "My Actors": pick from `Actor` populated by your account Actors.
  - If "From Store": pick from `Actor` populated by Apify Store (limit 1000).
- Input Body (`input_body`): Provide JSON for the Actor input.
- Optional query params:
  - `build`: specific build tag or id
  - `timeout` (seconds)
  - `memory` (MB): 512, 1024, 2048, 4096, 8192, 16384
  - `waitForFinish` (seconds, max 60): set 0 to no limit

The connector invokes `POST /v2/acts/{actorId}/runs` per Apify docs (see: https://docs.apify.com/api/v2/act-runs-post). The `actorId` path segment is chosen automatically based on your `actor_scope` selection.

### Run Task Action

Use the "Run Task" action to start an Apify Task run.

- Authentication: Use Apify API Key or OAuth 2.0 (scopes: `profile`, `full_api_access`).
- Headers: All requests include `x-apify-integration-platform: power-automate`.
- Task (`task_id`): Select the task from a dynamic dropdown of your available tasks.
- Input Body (`input_override`): Provide a raw JSON object to override the task's default input.
- Optional query params:
  - `timeout` (seconds)
  - `memory` (MB): 512, 1024, 2048, 4096, 8192, 16384
  - `waitForFinish` (seconds, max 60). If empty or 0, the call is asynchronous (does not wait).

The connector invokes `POST /v2/actor-tasks/{taskId}/runs` per Apify docs (see: https://docs.apify.com/api/v2/actor-task-runs-post). The `taskId` path segment is handled internally; you select the task via the `task_id` parameter.

## Testing

1. Create test flows in Power Automate to validate each action and trigger
2. Verify authentication works correctly
3. Test with various input parameters and edge cases
4. Validate output schema matches expectations

## CI/CD

The repository includes GitHub Actions workflows for:

1. **Validation**: Validates the connector files on every pull request
2. **Todo: Deployment**: Deploys the connector to the development environment on merge to main branch

### Workflow Files

- `.github/workflows/validate.yml`: Validates the connector files
- todo: `.github/workflows/deploy.yml`: Deploys the connector to the development environment

## Troubleshooting

### Common Issues

- **401 Unauthorized**: Verify your API token is valid and starts with `apify_`
- **Resource not found**: Check IDs and permissions; IDs are case sensitive
- **Timeouts**: For long-running tasks, increase timeout or disable wait_until_finish

### .NET Tools PATH Issues

If you encounter an error like this after installing the .NET tool:

```
Tools directory '/Users/username/.dotnet/tools' is not currently on the PATH environment variable.
```

You need to add the .NET tools directory to your PATH.

## Resources

- [Apify API Documentation](https://docs.apify.com/api/v2)
- [Microsoft Power Automate Documentation](https://docs.microsoft.com/en-us/power-automate/)
- [Power Platform Connectors Documentation](https://docs.microsoft.com/en-us/connectors/custom-connectors/)
- [Power Platform CLI Documentation](https://learn.microsoft.com/en-us/power-platform/developer/cli/introduction)
- [Custom Connector OpenAPI Definition](https://learn.microsoft.com/en-us/connectors/custom-connectors/define-openapi-definition)

---

Maintained by: Apify Team

License: MIT

Support: [GitHub Issues](https://github.com/apify/apify-microsoft-power-automate-integration/issues)

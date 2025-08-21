# Apify Microsoft Power Automate Integration

Connect your Microsoft Power Automate workflows with Apify's web scraping and automation platform. Run Actors and Tasks, fetch data from Datasets and Key-Value Stores, and trigger flows based on Apify webhooks.

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

This connector enables Microsoft Power Automate users to leverage Apify's web scraping and automation capabilities directly in their workflows. Apify provides serverless computing infrastructure for running web scraping, data extraction, and automation tasks called Actors.

## Prerequisites

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
   - Note: Some commands like `pac data` and certain `pac package` commands are only available on Windows

3. **Windows MSI** (Windows only):
   - Download and install from [Microsoft Download Center](https://aka.ms/PowerAppsCLI)
   - Enables all commands on Windows

### Verify Installation

To verify the installation:

```bash
# Check if pac is installed
pac

# Check the version
pac --version
```

## Development Setup

### Authentication Setup

Before working with the connector, you need to authenticate with your Power Platform environment:

1. **Create an authentication profile**:
   ```bash
   pac auth create --environment <Your-Environment-ID-or-URL>
   ```
   - This will open a browser window for you to log in
   - You need the Environment ID or URL from your Power Automate environment
   - Special access rights may be required; contact your tenant administrator if needed

2. **List available authentication profiles**:
   ```bash
   pac auth list
   ```

3. **Switch between profiles** (if you have multiple):
   ```bash
   pac auth select --index <profile-index>
   ```

### Clone Repository

```bash
git clone https://github.com/apify/apify-microsoft-power-automate-integration.git
cd apify-microsoft-power-automate-integration
```

## Connector Structure

```
.
├── src/
│   ├── swagger.json        # OpenAPI definition of the connector
│   ├── apiProperties.json  # Connector properties and metadata
│   ├── scripts.cs          # Custom connector scripts
│   └── icon.png            # Connector icon
├── .github/
│   └── workflows/          # CI/CD pipeline configurations
├── .gitignore              # Git ignore file
└── README.md               # Documentation
```

## Development Workflow

1. **Get the Connector ID**

   After creating a connector in your Power Automate environment, you'll need its ID for subsequent operations:

   ```bash
   pac connector list
   ```

   This will return a list of solution-aware connectors in your environment. Find your connector and copy its `ConnectorId`.

2. **Edit Locally**

   Make all changes to `swagger.json`, `apiProperties.json`, and `scripts.cs` in your local IDE.

3. **Deploy and Test in Power Automate**

   Use the `pac connector update` command to push your local files to the connector in your Power Automate environment:

   ```bash
   pac connector update \
     --connector-id <Your-Connector-ID> \
     --api-definition-file ./src/swagger.json \
     --api-properties-file ./src/apiProperties.json \
     --icon-file ./src/icon.png \
     --script-file ./src/scripts.cs
   ```

4. **Iterate**

   Use the "Test" tab in the Power Automate UI to test your changes. If you find issues, return to your local IDE, fix the files, and repeat the `pac connector update` command.

## Deployment

### Creating a New Connector

To create a new connector in your Power Automate environment:

```bash
pac connector create \
  --api-definition-file ./src/swagger.json \
  --api-properties-file ./src/apiProperties.json \
  --icon-file ./src/icon.png \
  --script-file ./src/scripts.cs \
  --solution-unique-name <your_solution_unique_name>
```

### Updating an Existing Connector

To update an existing connector:

```bash
pac connector update \
  --connector-id <Your-Connector-ID> \
  --api-definition-file ./src/swagger.json \
  --api-properties-file ./src/apiProperties.json \
  --icon-file ./src/icon.png \
  --script-file ./src/scripts.cs
```

## Testing

1. Create test flows in Power Automate to validate each action and trigger
2. Verify authentication works correctly
3. Test with various input parameters and edge cases
4. Validate output schema matches expectations

## CI/CD

The repository includes GitHub Actions workflows for:

1. **Validation**: Validates the connector files on every pull request
2. **Deployment**: Deploys the connector to the development environment on merge to main branch

### Workflow Files

- `.github/workflows/validate.yml`: Validates the connector files
- `.github/workflows/deploy.yml`: Deploys the connector to the development environment

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

You need to add the .NET tools directory to your PATH:

#### For Bash (macOS/Linux)
Add to your `~/.bashrc` or `~/.bash_profile`:
```bash
# Add .NET Core SDK tools
export PATH="$PATH:$HOME/.dotnet/tools"
```
Then run `source ~/.bashrc` (or `source ~/.bash_profile`) to apply changes to your current session.

#### For Zsh (macOS/Linux)
Add to your `~/.zshrc` or `~/.zprofile`:
```bash
# Add .NET Core SDK tools
export PATH="$PATH:$HOME/.dotnet/tools"
```
Then run `source ~/.zshrc` (or `source ~/.zprofile`) to apply changes to your current session.

#### For PowerShell (Windows)
Add to your PowerShell profile:
```powershell
# Add .NET Core SDK tools
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
```

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
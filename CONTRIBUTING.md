# Developer Guide

For developers contributing to or deploying this connector.

## Prerequisites

- [Apify account](https://apify.com)
- [Power Automate environment](https://make.powerautomate.com/)
- [Python](https://www.python.org/downloads) 3.5 or later

## Getting Started

### Clone Repository

```bash
git clone https://github.com/apify/apify-microsoft-power-automate-integration.git
cd apify-microsoft-power-automate-integration
```

### Install Power Platform Connectors CLI

`paconn` requires Python and is installed via pip:

1. Verify Python installation:

   ```bash
   python --version
   ```
2. *(optional)* Create and activate a Python virtual environment:
   ```bash
   python -m venv .venv
   source .venv/bin/activate
   ```

3. Install `paconn`:

   ```bash
   pip install paconn
   ```

4. Verify installation:

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

### Using `settings.json`

You can use a `settings.json` file in this project root to store arguments for paconn commands. This simplifies repeated operations because the CLI reads environment, connector ID, file paths, and other parameters from this file when provided.

This makes subsequent commands shorter:

```bash
paconn create --settings settings.json
paconn update --settings settings.json
```

## Connector Files

Apify's custom connector consists of the following core files:

| File | Description |
|------|-------------|
| `apiDefinition.swagger.json` | API description in OpenAPI/Swagger format, listing endpoints, inputs, and outputs that determine what actions and triggers appear in Power Automate |
| `apiProperties.json` | Connector metadata such as display name, authentication settings, host, and other configuration details |
| `scripts.csx` | C# script for custom request/response logic not covered by the API definition |
| `icon.png` | The image shown as the connector's icon in the Power Automate UI |

These definitions are stored locally and pushed to the Power Platform environment with paconn commands.

## Creating and Updating the Connector

### Create (First Time)

If the connector does not yet exist in your Power Automate environment, create it once:

```bash
paconn create --settings settings.json --secret <oauth-client-secret>
```

Or explicitly without settings:

```bash
paconn create -e <ENV_ID> --api-prop apiProperties.json --api-def apiDefinition.swagger.json --icon icon.png --script scripts.csx --secret <oauth-client-secret>
```

After creation, paconn prints the `connector ID`. Save this value in your `settings.json` for future updates.

### Update (Subsequent Changes)

Once the connector is created and you are modifying its definition locally, use the update command:

```bash
paconn update --settings settings.json --secret <oauth-client-secret>
```

Or explicitly:

```bash
paconn update -e <ENV_ID> -c <CONNECTOR_ID> --api-prop apiProperties.json --api-def apiDefinition.swagger.json --icon icon.png --script scripts.csx --secret <oauth-client-secret>
```

## Development Cycle

1. **Edit Locally**
   Update `apiDefinition.swagger.json`, `apiProperties.json`, and `scripts.csx` in your IDE.

2. **Deploy Updates**
   Push changes:

   ```bash
   paconn update --settings settings.json --secret <oauth-client-secret>
   ```

3. **Check for Errors**
   Go to custom connector edit mode in Power Automate and try saving the connector. If there are errors, check the error message, fix them locally and repeat.

4. **Test Changes**
   Run flows using your connector's actions and triggers to verify behavior.

5. **Repeat**
   Fix issues locally, then update and test again.

## Troubleshooting

**paconn command not found?**
- Ensure Python is installed and `pip install paconn` completed successfully
- Check that your Python scripts directory is in your PATH

**Authentication issues with paconn?**
- Run `paconn logout` then `paconn login` to refresh credentials
- Ensure you have the correct permissions in your Power Platform environment

**Connector update fails?**
- Verify the `connector ID` in `settings.json` matches your environment
- Check that the OAuth client secret is correct and not expired

**Changes not appearing in Power Automate?**
- Clear your browser cache or use incognito mode
- Try deleting and recreating the connection

## CI/CD Integration

The repository includes GitHub Actions workflows:

- **Validation:** Validates connector files on pull requests

## Resources

- [Apify API Documentation](https://docs.apify.com/api/v2)
- [Microsoft Power Automate Documentation](https://docs.microsoft.com/en-us/power-automate/)
- [Power Platform Connectors Documentation](https://docs.microsoft.com/en-us/connectors/custom-connectors/)
- [Power Platform Connectors CLI Documentation](https://learn.microsoft.com/en-us/connectors/custom-connectors/paconn-cli)

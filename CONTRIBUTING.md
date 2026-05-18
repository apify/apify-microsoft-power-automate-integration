# Developer Guide

For developers contributing to or deploying this connector.

## Prerequisites

- [Apify account](https://apify.com)
- [Power Automate environment](https://make.powerautomate.com/)
- [Python](https://www.python.org/downloads) 3.9 or later

## Getting Started

### Clone Repository

```bash
# Clone the repo
git clone https://github.com/apify/apify-microsoft-power-automate-integration.git
# Enter the project directory
cd apify-microsoft-power-automate-integration
```

### Install Power Platform Connectors CLI

`paconn` requires Python and is installed via pip:

1. Verify Python installation:

   ```bash
   # Check that Python 3.9+ is available
   python3 --version
   ```
2. *(optional)* Create and activate a Python virtual environment:
   ```bash
   # Create a virtual environment
   python3 -m venv .venv
   # Activate it
   source .venv/bin/activate
   ```

3. Install `paconn`:

   ```bash
   # Install the Power Platform Connectors CLI
   pip install paconn
   ```

4. Verify installation:

   ```bash
   # Should print usage help if installed correctly
   paconn
   ```

### Authentication

Authenticate with your Power Platform environment using device code login:

```bash
# Authenticate via device code flow
paconn login
```

Follow the prompt to open [https://login.microsoft.com/device](https://login.microsoft.com/device) and enter the code shown in your terminal.

To logout:

```bash
# Clear stored credentials
paconn logout
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

### Updating the icon

If you change `icon.png`, it must follow Microsoft's certification icon rules:

- PNG format, 1:1 aspect ratio, between **100×100 and 230×230 pixels**, no rounded edges.
- The logo content should occupy **less than 70%** of the image's width and height (i.e. leave consistent padding).
- Background is non-transparent, non-white (`#ffffff`), non-default (`#007ee5`), and matches the `iconBrandColor` value in `apiProperties.json`.
- `iconBrandColor` itself must be a valid hex color and also not `#ffffff` or `#007ee5`.
- The icon must be visually unique vs. other certified connectors.

See [Design an icon for your connector](https://learn.microsoft.com/en-us/connectors/custom-connectors/certification-submission#design-an-icon-for-your-connector-only-applicable-for-verified-publishers) for the canonical spec.

## Creating and Updating the Connector

> **Secret handling:** Never pass `--secret` on the command line - it leaks into shell history and process listings. Re-enter OAuth credentials in Power Automate after each deploy (see below).

### Re-entering OAuth credentials after deploy

Each time you create or update the connector without `--secret`, you need to re-configure the OAuth credentials in Power Automate:

1. Go to **Custom Connectors** in Power Automate.
2. Click **Edit** on the connector.
3. Navigate to the **Security** tab.
4. Under **OAuth 2.0**, click **Edit**.
5. Fill in the **Client ID** and **Client Secret**.
6. Save the connector.

### Create (First Time)

If the connector does not yet exist in your Power Automate environment, create it once:

```bash
# Create the connector in your environment
paconn create -e <ENV_ID> --api-prop apiProperties.json --api-def apiDefinition.swagger.json --icon icon.png --script scripts.csx
```

After creation, paconn prints the `connector ID`. You can pass it explicitly with `-c` in future commands, or omit it and paconn will prompt you to select the connector interactively.

> **Tip:** You can also use a [`settings.json`](https://learn.microsoft.com/en-us/connectors/custom-connectors/paconn-cli#settings-file) file to avoid repeating arguments on every command.

### Update (Subsequent Changes)

Once the connector is created and you are modifying its definition locally, use the update command:

```bash
# Push local changes to the existing connector
paconn update -e <ENV_ID> --api-prop apiProperties.json --api-def apiDefinition.swagger.json --script scripts.csx
```

Add `--icon icon.png` only when the icon has changed — it doesn't need to be re-uploaded every time.

## Development Cycle

1. **Edit Locally**
   Update `apiDefinition.swagger.json`, `apiProperties.json`, and `scripts.csx` in your IDE.

2. **Validate**
   Run before pushing - catches most issues `paconn update` would silently accept. Both must report clean; `paconn validate` is a thin client over Microsoft's certification Swagger Validator, so any warning here is a likely cert blocker.

   ```bash
   # Run local validation checks
   ./scripts/validate.sh
   # Run Microsoft's certification Swagger Validator
   paconn validate --api-def apiDefinition.swagger.json
   ```

3. **Deploy Updates**
   Push changes:

   ```bash
   # Deploy to Power Platform environment (add --icon icon.png only if the icon changed)
   paconn update -e <ENV_ID> --api-prop apiProperties.json --api-def apiDefinition.swagger.json --script scripts.csx
   ```

4. **Re-enter OAuth credentials**
   After each update, go to **Custom Connectors → Edit → Security → OAuth 2.0 → Edit** and fill in the Client ID and Client Secret, then save.

5. **Check for Errors**
   Go to custom connector edit mode in Power Automate and try saving the connector. If there are errors, check the error message, fix them locally and repeat.

6. **Test Changes**
   Run flows using your connector's actions and triggers to verify behavior.

7. **Repeat**
   Fix issues locally, then update and test again.

## Building and Submitting the Certification Package

Microsoft Partner Center accepts a single `ConnectorPackage.zip` per submission. This package includes solution exports from Power Apps (connector + sample flows) that can't be generated from the terminal - they must be exported through the Power Apps UI, tested in Power Automate, and then assembled locally by the developer before each release.

The submission archive has this nested structure (specified in [Microsoft's submission docs](https://learn.microsoft.com/en-us/connectors/custom-connectors/certification-submission)):

```
ConnectorPackage.zip
├── intro.md                   ← tracked in repo
└── package.zip
    └── PkgAssets/
        ├── ConnectorSolution.zip   ← exported from make.powerautomate.com
        └── FlowSolution.zip        ← exported from make.powerautomate.com
```

You will also need:
- **PowerShell 7+** (`brew install --cask powershell` on macOS) to run the package validator script.
- **`ConnectorPackageValidator.ps1`** - download from Microsoft's repo: [github.com/microsoft/PowerPlatformConnectors/blob/dev/scripts/ConnectorPackageValidator.ps1](https://github.com/microsoft/PowerPlatformConnectors/blob/dev/scripts/ConnectorPackageValidator.ps1).
- **An Azure Storage account** - Partner Center requires a SAS URI pointing to the uploaded package blob (not a direct file upload). Upload `ConnectorPackage.zip` to a container and generate a SAS URL valid for at least 15 days. See [Grant limited access with SAS](https://learn.microsoft.com/en-us/azure/storage/common/storage-sas-overview).

### One-time setup in Power Automate

The two inner zips come from **Power Automate solutions** you create in the same Power Platform environment that backs your Partner Center offer. Set them up once and reuse them for every submission. See [Solutions overview](https://learn.microsoft.com/en-us/power-apps/maker/data-platform/solutions-overview).

In [make.powerautomate.com](https://make.powerautomate.com/) → Solutions → **New solution**:

1. **Connector solution** (e.g. *"Apify Connector"*) - add only the Apify custom connector to it.
2. **Flow solution** (e.g. *"Apify Sample Flows"*) - add the Apify connector **plus** the sample template flows shown to Partner Center reviewers.

### For each submission

1. **Validate locally** - `paconn validate --api-def apiDefinition.swagger.json` must report clean. Catches most cert blockers ([Swagger Validator rules](https://learn.microsoft.com/en-us/connectors/custom-connectors/certification-swagger-validator-rules)).
2. **Push the connector** — `paconn update -e <ENV_ID> --api-prop apiProperties.json --api-def apiDefinition.swagger.json --script scripts.csx` (add `--icon icon.png` only if the icon changed). Use the published connector that the Partner Center offer is built on.
3. **Smoke-test in Power Automate** - quick flow for each user-facing action (Run Actor, Scrape single URL, Get key-value store record, Actor run finished trigger + Delete actor webhook). The submission inherits whatever bugs the live connector has.
4. **Export both solutions** from Power Automate:
   1. In [make.powerautomate.com](https://make.powerautomate.com) → Solutions, open one of the two solutions you set up.
   2. Click **Export solution** in the toolbar. Power Automate prompts you to **Publish all customizations** first - do it, or the export ships stale data.
   3. After publishing, the export dialog opens. Version auto-increments (`1.0.0.N`); leave it. Pick **Unmanaged** (Microsoft's cert team requires it; "Managed (recommended)" is for production deploys, not submissions). Tick **Run solution checker on export** - Microsoft runs this server-side anyway, so failing early saves time.
   4. Click **Export**. A banner appears at the top of the page with a download link once the export and solution check complete (takes a couple of minutes).
   5. Save the downloaded zip as `ConnectorSolution.zip` (or `FlowSolution.zip` for the other solution).
   6. Repeat for the second solution.

   See [Export solutions](https://learn.microsoft.com/en-us/power-apps/maker/data-platform/export-solutions) for the full reference.
5. **Build the package zips**:
   ```bash
   # Create the package directory structure
   mkdir -p ConnectorPackage/PkgAssets
   # Move exported solutions into the package
   mv /path/to/ConnectorSolution.zip /path/to/FlowSolution.zip ConnectorPackage/PkgAssets/
   # Bundle PkgAssets/ into package.zip
   cd ConnectorPackage && zip -r package.zip PkgAssets/
   # Copy intro.md alongside package.zip and assemble the final submission archive
   cp ../intro.md . && zip ConnectorPackage.zip package.zip intro.md && cd ..
   ```
6. **Run the package validator**:

   ```bash
   # Note: needs absolute zip path; the second arg is isPluginEnabled (y/n) - use n for a connector
   pwsh -File ConnectorPackage/ConnectorPackageValidator.ps1 "$(pwd)/ConnectorPackage/ConnectorPackage.zip" n
   ```

   Expected output: `Validation successful: The package structure is correct.`
7. **Upload to Azure blob storage** and generate a SAS URL (valid ≥15 days) - see the [Azure Storage prerequisite](#building-and-submitting-the-certification-package) above.
8. **Submit via Partner Center** - [partner.microsoft.com/dashboard](https://partner.microsoft.com/dashboard) → Marketplace offers → Apify connector. On the **Packages** tab, paste the SAS URI. For updates, use **Resubmit** (don't create a new offer). See [Submit a connector for certification](https://learn.microsoft.com/en-us/connectors/custom-connectors/submit-for-certification). In the submission notes, summarize what changed and any previously-failed policy codes addressed.

If a submission fails, the error includes a policy code you can look up in the [policy errors reference](https://learn.microsoft.com/en-us/connectors/custom-connectors/certification-policy-errors). For unclear failures, Microsoft holds [Office Hours](https://learn.microsoft.com/en-us/connectors/custom-connectors/certification-submission#for-queries-regarding-certification) every Tuesday 15:30–16:30 UTC where engineers can read the validator's activity log directly.

## Troubleshooting

**paconn command not found?**
- Check that your Python scripts directory is in your PATH
- Check that your virtual environment is activated (`source .venv/bin/activate`)
- Ensure Python is installed and `pip install paconn` completed successfully

**Authentication issues with paconn?**
- Run `paconn logout` then `paconn login` to refresh credentials
- Ensure you have the correct permissions in your Power Platform environment

**Connector update fails?**
- Ensure the environment ID provided with the `-e` argument is correct. You can find your environment ID in the URL: https://make.powerautomate.com/environments/<environment_id>

**Changes not appearing in Power Automate?**
- Clear your browser cache or use incognito mode
- Try deleting and recreating the connection

## CI/CD Integration

The repository runs a GitHub Actions workflow (`.github/workflows/validate.yml`) on every push. It executes `./scripts/validate.sh` which checks connector file structure and required fields. To run the same checks locally:

```bash
# Run the same validation CI uses
./scripts/validate.sh
```

## Resources

- [Apify API Documentation](https://docs.apify.com/api/v2)
- [Microsoft Power Automate Documentation](https://learn.microsoft.com/en-us/power-automate/)
- [Power Platform Connectors Documentation](https://learn.microsoft.com/en-us/connectors/custom-connectors/)
- [Power Platform Connectors CLI Documentation](https://learn.microsoft.com/en-us/connectors/custom-connectors/paconn-cli)
- [Submit a connector for certification (verified publisher)](https://learn.microsoft.com/en-us/connectors/custom-connectors/submit-for-certification)
- [Certification policy errors](https://learn.microsoft.com/en-us/connectors/custom-connectors/certification-policy-errors) - `5000.x` error code reference
- [Swagger Validator rules](https://learn.microsoft.com/en-us/connectors/custom-connectors/certification-swagger-validator-rules) - the rule names paconn reports
- [Policy templates reference](https://learn.microsoft.com/en-us/connectors/custom-connectors/policy-templates) - `setheader`, `routerequesttoendpoint`, etc.

---

**Maintained by:** Apify Team
**Support:** [GitHub Issues](https://github.com/apify/apify-microsoft-power-automate-integration/issues)

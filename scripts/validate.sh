#!/bin/bash

# Validate Power Automate connector structure and required files

# Check required files exist
for file in apiDefinition.swagger.json apiProperties.json scripts.csx icon.png; do
  if [ ! -f "./$file" ]; then
    echo "Error: $file file not found"
    exit 1
  fi
done

# Validate JSON files
echo "Validating apiDefinition.swagger.json..."
jq -e '.swagger and .info and .info.title and .info.version and .host and .basePath' ./apiDefinition.swagger.json > /dev/null || { echo "Error: apiDefinition.swagger.json is invalid or missing required fields"; exit 1; }

echo "Validating apiProperties.json..."
jq -e '.properties and .properties.connectionParameters' ./apiProperties.json > /dev/null || { echo "Error: apiProperties.json is invalid or missing required fields"; exit 1; }

# Check script contains required class
echo "Validating scripts.csx..."
grep -q "public class Script : ScriptBase" ./scripts.csx || { echo "Error: scripts.csx does not contain the required Script class"; exit 1; }

# Validate icon is PNG
echo "Validating icon.png..."
file ./icon.png | grep -q "PNG image data" || { echo "Error: icon.png is not a valid PNG file"; exit 1; }

echo "All connector files validated successfully"

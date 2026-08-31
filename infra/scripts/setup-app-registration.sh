#!/bin/bash

set -euo pipefail

if ! az account show >/dev/null 2>&1; then
    echo "You must be logged in to Azure to run this script"
    echo "Run 'az login' to log in to Azure"
    exit 1
fi

echo "Loading azd .env file from current environment"

# Use the `get-values` azd command to retrieve environment variables from the `.env` file
while IFS='=' read -r key value; do
    [ -z "${key}" ] && continue
    value=$(echo "$value" | sed 's/^"//' | sed 's/"$//')
    export "$key=$value"
done <<EOF
$(azd env get-values)
EOF

AUTH_APP_NAME="$AZURE_ENV_NAME-app"

# Allow opting out of Entra ID entirely (e.g. tenants where admin consent cannot be granted).
# When ADMIN_AUTH_MODE=password we clear entraClientId so the Bicep falls back to local auth.
if [ "${ADMIN_AUTH_MODE:-entra}" = "password" ]; then
    echo "ADMIN_AUTH_MODE=password - skipping Entra ID app registration"
    azd env set entraClientId ""
    azd env set entraTenantId ""
    exit 0
fi

app_id=$(az ad app list --filter "displayname eq '$AUTH_APP_NAME'" --query "[0].appId" -o tsv 2>/dev/null || true)

if [ -z "${app_id}" ]; then
    echo "Creating app registration for $AUTH_APP_NAME"
    app_id=$(az ad app create \
        --display-name "$AUTH_APP_NAME" \
        --sign-in-audience AzureADMyOrg \
        --enable-id-token-issuance true \
        --query appId -o tsv)
else
    echo "App registration for $AUTH_APP_NAME already exists"

    # Ensure ID token issuance is enabled
    az ad app update --id "$app_id" --enable-id-token-issuance true >/dev/null 2>&1 || true
fi

if [ -z "${app_id}" ]; then
    echo "Failed to create or locate the app registration for $AUTH_APP_NAME"
    echo "Ensure you have permission to register applications in this tenant,"
    echo "or run 'az login --scope https://graph.microsoft.com//.default' and retry."
    exit 1
fi

tenantId=$(az account show --query tenantId -o tsv)

echo "Adding environment variables to azd environment"
azd env set entraClientId "$app_id"
azd env set entraTenantId "$tenantId"

echo "App Registration complete (clientId=$app_id, tenantId=$tenantId)"

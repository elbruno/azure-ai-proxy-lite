#!/bin/bash

# Git Bash / MSYS on Windows rewrites arguments that look like POSIX paths (e.g. "/signin-oidc")
# into Windows paths, which corrupts the AzureAd__CallbackPath value. Disable that conversion.
export MSYS_NO_PATHCONV=1
export MSYS2_ARG_CONV_EXCL='*'

if ! az account show >/dev/null 2>&1; then
    echo "You must be logged in to Azure to run this script"
    echo "Run 'az login' to log in to Azure"
    exit 1
fi

echo "Loading azd .env file from current environment"

# Use the `get-values` azd command to retrieve environment variables from the `.env` file
while IFS='=' read -r key value; do
    [ -z "$key" ] && continue
    value=$(echo "$value" | sed 's/^"//' | sed 's/"$//')
    export "$key=$value"
done <<EOF
$(azd env get-values)
EOF

# Use entraClientId (new) or AUTH_CLIENT_ID (legacy) variable name
CLIENT_ID="${entraClientId:-${AUTH_CLIENT_ID:-}}"

if [ -z "$CLIENT_ID" ]; then
    echo "No Entra app registration configured (entraClientId not set). Skipping redirect URI setup."
    exit 0
fi

signin_path='/signin-oidc'

echo "Ensuring redirect URIs for app registration $CLIENT_ID"

if [ -n "${SERVICE_ADMIN_URI:-}" ]; then
    desired_uri="${SERVICE_ADMIN_URI}${signin_path}"

    # -o tsv returns one redirect URI per line.
    existing_redirects=$(az ad app show --id "$CLIENT_ID" --query "web.redirectUris[]" -o tsv 2>/dev/null || true)

    if echo "$existing_redirects" | grep -Fxq "$desired_uri"; then
        echo "  $desired_uri already registered"
    else
        echo "  Registering $desired_uri"
        # shellcheck disable=SC2086
        az ad app update --id "$CLIENT_ID" \
            --web-redirect-uris $existing_redirects "$desired_uri" \
            --output none
    fi
else
    echo "SERVICE_ADMIN_URI not set — skipping redirect URI setup"
fi

echo "Redirect URI setup complete"

# Ensure the admin container app has the AzureAd env vars set.
# azd deploy creates new revisions that may not carry forward bicep-provisioned env vars,
# so we set them directly on the container app to ensure they persist.
if [ -n "${SERVICE_ADMIN_NAME:-}" ] && [ -n "$CLIENT_ID" ]; then
    TENANT_ID="${entraTenantId:-${AUTH_TENANT_ID:-}}"
    ADMIN_RG="${AZURE_RESOURCE_GROUP:-${AZURE_ENV_NAME}-rg}"
    echo "Ensuring AzureAd env vars on admin container app $SERVICE_ADMIN_NAME"
    az containerapp update \
        -n "$SERVICE_ADMIN_NAME" \
        -g "$ADMIN_RG" \
        --set-env-vars \
            "AzureAd__Instance=https://login.microsoftonline.com/" \
            "AzureAd__TenantId=$TENANT_ID" \
            "AzureAd__ClientId=$CLIENT_ID" \
            "AzureAd__CallbackPath=/signin-oidc" \
        --output none 2>&1
    echo "Admin container app env vars updated"
fi

#!/bin/bash

set -euo pipefail

echo "Checking azd environment for ENCRYPTION_KEY"

# `azd env get-value` writes its "key not found" error to stdout and exits non-zero,
# so the exit code (not the captured text) is what tells us whether the key exists.
if existing_key="$(azd env get-value ENCRYPTION_KEY 2>/dev/null)"; then
    existing_key="$(printf '%s' "${existing_key}" | tr -d '\r\n"')"
else
    existing_key=""
fi

if [ -n "${existing_key}" ]; then
    echo "ENCRYPTION_KEY already set in azd environment"
    exit 0
fi

if ! command -v openssl >/dev/null 2>&1; then
    echo "openssl is required to generate ENCRYPTION_KEY automatically"
    exit 1
fi

generated_key="$(openssl rand -hex 32)"

echo "Generating ENCRYPTION_KEY and saving it to the azd environment"
azd env set ENCRYPTION_KEY "${generated_key}"

echo "ENCRYPTION_KEY generated and stored"

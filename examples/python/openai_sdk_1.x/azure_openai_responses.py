"""Smoke test the Azure AI Proxy Responses API with the OpenAI Python SDK.

Set these values from the event registration page or downloaded .env file:

    PROXY_ENDPOINT=https://<proxy-host>/api/v1
    PROXY_API_KEY=<event-api-key>
    MODEL_NAME=gpt-5-mini
"""

import os

from dotenv import load_dotenv
from openai import AzureOpenAI


load_dotenv()

endpoint = os.environ.get("PROXY_ENDPOINT")
api_key = os.environ.get("PROXY_API_KEY")
model_name = os.environ.get("MODEL_NAME", "gpt-5-mini")

if not endpoint or not api_key:
    raise SystemExit(
        "Set PROXY_ENDPOINT and PROXY_API_KEY before running this sample."
    )

client = AzureOpenAI(
    azure_endpoint=endpoint,
    api_key=api_key,
    api_version="2025-04-01-preview",
)

response = client.responses.create(
    model=model_name,
    input="Reply with one short sentence confirming the Azure AI Proxy works.",
    max_output_tokens=256,
)

print(response.output_text)

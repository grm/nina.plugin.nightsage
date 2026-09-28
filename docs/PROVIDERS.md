# AI provider setup

NightSage needs access to an LLM to discover targets and build acquisition plans. You can use a cloud provider or an OpenAI-compatible local/remote endpoint.

API keys are encrypted locally with Windows DPAPI for the current Windows user. NightSage does not provide, resell or proxy API access. Provider usage, quotas and billing are controlled by the provider.

> A paid consumer chat subscription does not necessarily include API usage. Check the API billing and quota settings for the provider you select.

## OpenAI

1. Open the official OpenAI API-key help page:
   https://help.openai.com/en/articles/4936850-where-do-i-find-my-secret-api-key
2. Create or select an API project and create a secret API key.
3. In NightSage, choose **OpenAI**.
4. Paste the key into **API key**.
5. Leave **Custom API endpoint** blank unless you deliberately use a different OpenAI-compatible gateway.
6. Click **Test provider**.

OpenAI recommends treating API keys as secrets and rotating them if they are exposed.

## Anthropic

1. Open the Claude Platform API-key page:
   https://platform.claude.com/settings/keys
2. Sign in to Claude Console and create an API key.
3. In NightSage, choose **Anthropic**.
4. Paste the key into **API key**.
5. Leave **Custom API endpoint** blank for the standard Anthropic API.
6. Click **Test provider**.

Claude consumer subscriptions and Claude API usage are separate products; verify API billing in Claude Console.

## Google Gemini

1. Open the official Gemini API-key documentation:
   https://ai.google.dev/gemini-api/docs/api-key
2. Create or manage a Gemini API key in Google AI Studio.
3. In NightSage, choose **Google Gemini**.
4. Paste the key into **API key**.
5. Leave **Custom API endpoint** blank for the standard Gemini API.
6. Click **Test provider**.

Google's key requirements can change over time, so use the linked Google documentation rather than relying on old setup instructions.

## OpenAI-compatible endpoints

Choose **OpenAI-compatible** when the service exposes an OpenAI-style `/v1/chat/completions` API.

NightSage accepts either a base URL ending in `/v1` or a full URL ending in `/chat/completions`. If you enter a base URL, NightSage appends `/chat/completions` automatically.

The **Model** field must contain the model identifier expected by that server. The **API key** can be left empty when the server does not require authentication.

### Ollama

Typical NightSage settings:

```text
Provider: OpenAI-compatible
Custom API endpoint: http://localhost:11434/v1
Model: <your Ollama model name>
API key: leave blank unless your setup requires one
```

Ollama documentation:
https://docs.ollama.com/openai

### LM Studio

Start the local server from LM Studio's Developer tab.

Typical NightSage settings:

```text
Provider: OpenAI-compatible
Custom API endpoint: http://localhost:1234/v1
Model: <the model identifier shown by LM Studio>
API key: leave blank unless authentication is enabled
```

LM Studio OpenAI-compatible API documentation:
https://lmstudio.ai/docs/developer/openai-compat

### OpenRouter

```text
Provider: OpenAI-compatible
Custom API endpoint: https://openrouter.ai/api/v1
Model: <provider/model slug>
API key: <your OpenRouter API key>
```

OpenRouter developer documentation:
https://openrouter.ai/developers

### Groq

```text
Provider: OpenAI-compatible
Custom API endpoint: https://api.groq.com/openai/v1
Model: <a Groq-supported model ID>
API key: <your Groq API key>
```

Groq OpenAI-compatibility documentation:
https://console.groq.com/docs/openai

### Mistral

```text
Provider: OpenAI-compatible
Custom API endpoint: https://api.mistral.ai/v1
Model: <a Mistral model ID>
API key: <your Mistral API key>
```

Mistral API-key documentation:
https://docs.mistral.ai/en/admin/identity-access/api-keys

## Troubleshooting

If **Test provider** fails:

- verify the API key has not expired or been revoked;
- verify API billing/quota is enabled where required;
- verify the model identifier exists for that provider;
- for OpenAI-compatible servers, verify the server is running and reachable from the N.I.N.A. machine;
- verify the endpoint is the provider's OpenAI-compatible base URL, not a web dashboard URL;
- if you use a local server, test `http://localhost:<port>/v1/models` from the same machine.

Do not post API keys in GitHub issues, screenshots, N.I.N.A. logs or support messages.

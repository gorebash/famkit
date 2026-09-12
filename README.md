# FamKit

Family Kit is a Kanban board specifically designed for families and kids!

## Fridge-to-Meal-Plan Assistant (prototype)

Snap a photo of your fridge/pantry, an Azure AI Foundry vision model identifies the ingredients, and the app
calls the Spoonacular recipe API to suggest meals and build a grocery list for whatever's missing. You can also
enter a recipe by hand and see what you're missing from your current stock.

### Structure

- `src/famkit.slnx` — solution file.
- `src/famkit.Api` — Azure Functions (.NET 10 isolated worker, hosted model). Vision identification, pantry/recipe
  CRUD (Azure Table Storage), Spoonacular integration, and the ingredient-diff logic.
- `src/famkit.Web` — React + Vite + TypeScript frontend.

### Local development

Requirements: .NET 10 SDK, Azure Functions Core Tools v4, Node.js, and [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite)
for local Table Storage.

```bash
# Terminal 1: local Table Storage emulator
npx azurite --silent --location .azurite

# Terminal 2: API
cd src/famkit.Api
cp local.settings.json.example local.settings.json   # fill in Foundry + Spoonacular values
func start

# Terminal 3: web app
cd src/famkit.Web
npm install
cp .env.example .env
npm run dev
```

The web app defaults to `http://localhost:7071/api` for the API — override with `VITE_API_BASE_URL` in `.env`.

Vision identification, chat, and meal suggestions require real credentials in `src/famkit.Api/local.settings.json`:
- `Foundry:Endpoint` / `Foundry:ApiKey` / `Foundry:DeploymentName` / `Foundry:ChatDeploymentName` — an Azure AI
  Foundry resource with two deployments: `image-processor` (vision) and `chat-processor` (tool-calling chat).
  Use the resource's classic endpoint shape (`https://<resource-name>.openai.azure.com/`), not the
  `/api/projects/<name>` project endpoint — `Azure.AI.OpenAI`'s `AzureOpenAIClient` needs the former for API-key auth.

  Azure OpenAI model availability (gpt-4o/gpt-5 family) is **regional**, not a subscription-wide gate — many
  regions (this project's original `westus2` resource included) have zero OpenAI-format models in their catalog,
  only third-party ones (Meta, Microsoft, Cohere, DeepSeek, xAI, etc). `eastus` reliably has the full OpenAI
  catalog. This project's active resource, `famkit-foundry-2` (`eastus`), runs **gpt-5-mini** on both deployments
  — it's natively multimodal (vision) and supports real tool calling, so one model now covers both jobs (earlier
  iterations needed Llama-4-Scout for vision + gpt-oss-120b for tool calling as a workaround). gpt-5-mini is also
  a reasoning model: if you ever see empty responses with `finish_reason: "length"`, the reasoning tokens ate the
  whole budget — either raise `max_completion_tokens` or pass `reasoning_effort: "minimal"` for straightforward
  extraction tasks like ours.

  A prior resource, `famkit-foundry` (`westus2`, Llama-4-Scout + gpt-oss-120b), is still provisioned but no longer
  referenced by the app — kept around temporarily, slated for deletion.
- `Spoonacular:ApiKey` — a free-tier key from [spoonacular.com/food-api](https://spoonacular.com/food-api).

Pantry and recipe management work without those keys.

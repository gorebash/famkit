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

Vision identification and meal suggestions require real credentials in `src/famkit.Api/local.settings.json`:
- `Foundry:Endpoint` / `Foundry:ApiKey` / `Foundry:DeploymentName` — an Azure AI Foundry vision-capable model
  deployment. Use the resource's classic endpoint shape (`https://<resource-name>.openai.azure.com/`), not the
  `/api/projects/<name>` project endpoint — `Azure.AI.OpenAI`'s `AzureOpenAIClient` needs the former for API-key auth.
  This project's `famkit-foundry` resource has no Azure OpenAI (gpt-4o family) access, so it's deployed on
  **Llama-4-Scout-17B-16E-Instruct** (Meta, natively multimodal, `GlobalStandard` SKU) under the deployment name
  `image-processor` instead — swap in `gpt-4o-mini` there if/when OpenAI models become available on the resource.
- `Spoonacular:ApiKey` — a free-tier key from [spoonacular.com/food-api](https://spoonacular.com/food-api).

Pantry and recipe management work without those keys.

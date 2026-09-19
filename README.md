# FamKit

Family Kit is a Kanban board specifically designed for families and kids!

## Fridge-to-Meal-Plan Assistant (prototype)

Snap a photo of your fridge/pantry, an Azure AI Foundry vision model identifies the ingredients, and the app
calls the Spoonacular recipe API to suggest meals and build a grocery list for whatever's missing. You can also
enter a recipe by hand and see what you're missing from your current stock.

### Structure

- `src/famkit.slnx` — solution file.
- `src/famkit.Api` — Azure Functions (.NET 8 isolated worker, hosted model). Vision identification, pantry/recipe
  CRUD (Azure Table Storage), Spoonacular integration, chat, and the ingredient-diff logic. This is the project
  that gets deployed (see Deployment below) — HTTP-triggered functions only.
- `src/famkit.Mcp` — a separate, **local-only** Azure Functions project exposing the same pantry/recipe operations
  as MCP tools (`famkit-mcp`, for VS Code Copilot / Claude Desktop / MCP Inspector). It project-references
  `famkit.Api` to reuse `PantryRepository`/`RecipeRepository` against the same Table Storage data, so both
  surfaces see identical state. It's split out because Azure Static Web Apps' managed Functions integration only
  supports `httpTrigger` functions — an `mcpToolTrigger` function anywhere in the deployed project fails the
  build (see Deployment). Run it locally with `func start` in this folder; it's never deployed.
- `src/famkit.Web` — React + Vite + TypeScript frontend.

### Local development

Requirements: .NET SDK (8.0+), Azure Functions Core Tools v4, Node.js, and [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite)
for local Table Storage.

```bash
# Terminal 1: local Table Storage emulator
npx azurite --silent --location .azurite

# Terminal 2: API
cd src/famkit.Api
cp local.settings.json.example local.settings.json   # fill in Foundry + Spoonacular values
func start   # http://localhost:7072

# Terminal 3: MCP tools (optional — only needed for VS Code Copilot / Claude Desktop / MCP Inspector)
cd src/famkit.Mcp
func start   # http://localhost:7073, registered in .vscode/mcp.json

# Terminal 4: web app
cd src/famkit.Web
npm install
cp .env.example .env
npm run dev
```

The web app defaults to `http://localhost:7071/api` for the API — override with `VITE_API_BASE_URL` in `.env`.

Vision identification, chat, and meal suggestions require real credentials in `src/famkit.Api/local.settings.json`:
- `Foundry:Endpoint` / `Foundry:ApiKey` / `Foundry:DeploymentName` — the Foundry resource's classic endpoint
  (`https://<resource-name>.openai.azure.com/`, not the `/api/projects/<name>` project endpoint) and the
  `image-processor` deployment, used directly by `FoundryVisionService` via `Azure.AI.OpenAI`'s
  `AzureOpenAIClient` with API-key auth.
- `Foundry:ChatDeploymentName` — the `chat-processor` deployment name. Only used as the model backing the
  Foundry agent below; nothing in this app's code calls it directly anymore.
- `Foundry:ProjectEndpoint` / `Foundry:ChatAgentName` — chat is handled by a **Foundry Prompt Agent**
  (`FamKitChatAgent`), not a direct model call. `ChatService` calls it over the Responses API via
  `Azure.AI.Projects` + `Azure.AI.Extensions.OpenAI`, using the project endpoint
  (`https://<resource-name>.services.ai.azure.com/api/projects/<project-name>`) and `DefaultAzureCredential`
  (Entra ID — resolves via `az login` locally; this is a different auth model from the API-key path vision
  uses, because Foundry's Agent Service requires it). The agent owns its own system prompt and tool JSON
  schemas — they live in Foundry (editable via the portal or the agent REST API), not in C# — and the tools
  it can call are `list_pantry`, `add_pantry_item`, `list_recipes`, `evaluate_recipe_ingredients`, mirroring the
  MCP tools below. `ChatService` runs the actual tool-calling round trip: send the conversation, execute
  whatever the agent asks for against the real repositories, feed the result back, repeat until it produces a
  final reply. First chat request in a fresh process run is slow (~30-45s) — that's `DefaultAzureCredential`
  probing its full credential chain once; it's cached for the rest of the process's lifetime.

  Conversation state is chained via the Responses API's `previous_response_id`, not resent by the client.
  `POST /api/chat` takes `{ message, previousResponseId? }` and returns `{ reply, toolActivity, responseId }` —
  the frontend only has to remember the last `responseId` and echo it back next turn (see `ChatPanel` in
  `HomePage.tsx`). This also applies *within* a single turn's tool round trips (each iteration chains off the
  previous one and sends only the new tool output, not the whole growing history). Without this, the client
  would have to resend full message history on every turn, and the model — having no memory of its own past
  tool calls — would re-invoke tools like `list_pantry` on every new message even when nothing changed,
  burning tokens and latency for no reason. With chaining, it still calls tools again when there's an actual
  reason to (e.g. being asked to double-check), it just stops doing so reflexively.

  Azure OpenAI model availability (gpt-4o/gpt-5 family) is **regional**, not a subscription-wide gate — many
  regions (this project's original `westus2` resource included) have zero OpenAI-format models in their catalog,
  only third-party ones (Meta, Microsoft, Cohere, DeepSeek, xAI, etc). `eastus` reliably has the full OpenAI
  catalog, including Responses/Agent support. This project's active resource, `famkit-foundry-2` (`eastus`),
  runs **gpt-5-mini** on both deployments — it's natively multimodal (vision) and supports real tool calling,
  so one model covers both jobs (earlier iterations needed Llama-4-Scout for vision + gpt-oss-120b for tool
  calling as a workaround, and Foundry Prompt Agents didn't work at all without an Azure OpenAI model backing
  them). gpt-5-mini is also a reasoning model — both `FoundryVisionService` and `ChatService` pass
  `reasoning_effort: "minimal"` since these are straightforward extraction/tool-calling tasks that don't need
  deep reasoning; without it the model can burn its whole token budget on internal reasoning before producing
  any output (`finish_reason: "length"`, empty content).

  A prior resource, `famkit-foundry` (`westus2`, Llama-4-Scout + gpt-oss-120b), is still provisioned but no longer
  referenced by the app — kept around temporarily, slated for deletion.
- `Spoonacular:ApiKey` — a free-tier key from [spoonacular.com/food-api](https://spoonacular.com/food-api).

Pantry and recipe management work without those keys.

### Deployment

Hosted on [Azure Static Web Apps](https://azure.microsoft.com/products/app-service/static), Free tier, via
`.github/workflows/azure-static-web-apps.yml` — pushes to `main` build `famkit.Web` and deploy it alongside
`famkit.Api` using SWA's built-in **managed Functions integration** (no separate Function App resource, no
extra cost beyond Free tier's $0). The frontend is built with `VITE_API_BASE_URL=/api`, which SWA rewrites
same-origin to the linked Functions app.

App settings (Foundry/Spoonacular keys) are configured directly on the Static Web App resource
(`az staticwebapp appsettings set`), not in `local.settings.json` (that file is gitignored and local-only).

Two Free-tier constraints surfaced while setting this up, both from SWA's managed integration using a
sandboxed Oryx build rather than a real standalone Function App:
1. **`.NET` version is capped** — Oryx's managed Functions build only supports `dotnet-isolated` 8.0/9.0, not
   10.0 (`famkit.Api` was downgraded from net10.0 to net8.0 for this; nothing in the code needed net10).
2. **Only `httpTrigger` functions are supported** — an `mcpToolTrigger` function anywhere in the deployed
   project fails the build outright (`invalid trigger of type 'mcpToolTrigger'... only httpTriggers are
   supported`). This is why `famkit.Mcp` is a separate, non-deployed project (see Structure above) rather than
   a folder inside `famkit.Api`.
3. **`AzureWebJobsStorage` can't be pointed at your own storage account** — the platform reserves that name for
   its own internal Functions runtime storage and rejects any attempt to set it (`AppSetting with name(s)
   'AzureWebJobsStorage' are not allowed`). Table Storage for actual app data (pantry/recipes) uses its own
   config key instead — `TableStorage:ConnectionString` — pointed at a dedicated storage account
   (`famkitappdata`, provisioned separately from any other resource in the resource group) via
   `az staticwebapp appsettings set`.

A linked-backend architecture (a real standalone Function App, keeping net10 + MCP intact, fronted by the SWA)
was considered and rejected for cost reasons — linked backends require the SWA **Standard** tier (~$9/mo)
regardless of which compute sits behind it, which didn't fit this project's low-traffic personal-use budget.

Auth (the SWA is already configured with OAuth) is intentionally not yet wired into the app — planned as a
separate follow-up.

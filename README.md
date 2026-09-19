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

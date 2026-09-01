# Documentation improvement plan

## Goal

Make the documentation work for two reading modes:

1. **Experienced users** should be able to complete each task from a short TL;DR.
2. **First-time users** should be able to continue into the detailed walkthrough, screenshots,
   explanations, validation, and troubleshooting.

The two primary journeys are:

- **Event administrator**: deploy the solution, configure Azure resources, create an event, validate
  capacity, rehearse the attendee flow, and monitor the event.
- **Event attendee**: register, copy event values, configure the GitHub Copilot App, validate the
  model, and resolve common errors.

## Standard page structure

Every task-oriented documentation page should use this order:

1. **TL;DR** — the minimum commands, fields, or clicks needed by an experienced user.
2. **Before you start** — permissions, tools, values, and prerequisites.
3. **Detailed walkthrough** — numbered steps with screenshots where UI interaction matters.
4. **Verify** — one concrete check with the expected result.
5. **Troubleshooting** — task-specific symptoms, causes, and fixes.
6. **Next step** — one link to the next page in the role journey.

### TL;DR rules

- Keep it to 3–7 steps whenever possible.
- Include exact commands and exact field values.
- State where dynamic values come from instead of asking users to reconstruct them.
- Put warnings immediately beside the affected setting.
- Include one expected success result.
- Link to the detailed section rather than repeating all background information.
- Do not mix administrator and attendee actions in the same TL;DR.

## Phase 1 — consistency and navigation

### Main README

- [x] Add role-based entry points for administrators and attendees.
- [x] Link directly to deployment, resources, events, capacity, load testing, registration,
  Copilot App setup, and SDK samples.
- [x] Fix broken repository-relative documentation and image links.
- [ ] Add a short “Choose your path” diagram or two-card layout to the published documentation home.

### Cross-document consistency

- [x] Resolve the environment-name conflict: some pages say 7 characters and others say 12.
- [x] Resolve the attendee-authentication conflict: GitHub registration is the normal path, but
  shared-code access does not require GitHub.
- [x] Document Entra ID and local-admin authentication as explicit supported modes.
- [ ] Replace stale `gpt-4.1-mini`, `gpt-4o`, and `2024-10-21` examples where GPT-5/Responses is the
  intended current path.
- [ ] Consolidate or archive legacy pages that duplicate current deployment and testing guidance.
- [ ] Establish a documentation publishing path for this fork so new MkDocs pages are publicly
  available instead of only rendered on GitHub.

## Phase 2 — event administrator journey

| Order | Page | TL;DR content |
|---:|---|---|
| 1 | `deployment/azure.md` | Login, select subscription, create/set azd environment, configure encryption, run `azd up`, retrieve three service URLs. |
| 2 | `deployment/managed_identity.md` | Identify proxy managed identity, assign the required role at the correct scope, enable Managed Identity on the resource, test access. |
| 3 | `resources.md` | Choose the resource type, enter deployment/model name, use the correct endpoint format, select authentication, save, verify active status. |
| 4 | `events.md` | Create event, attach resources, set time window and caps, save, copy attendee URL. |
| 5 | `capacity.md` | Estimate users × prompt rate × reserved tokens, compare with TPM/RPM, apply tested Copilot profiles, scale before rehearsal. |
| 6 | Load testing | Install dependency, set event key, run 10/25/50 stages, interpret SSE failures, compare with Azure Monitor. |
| 7 | `reporting.md` | Select event, confirm registrations/requests/tokens, identify resource saturation, export or capture results. |

### Administrator quickstart checklist

- [ ] Add one canonical “Deploy to event-ready” page linking the seven steps above.
- [ ] Add a post-deployment health check for proxy, admin, and registration endpoints.
- [ ] Add a resource configuration matrix:
  resource type, endpoint format, API version, authentication method, RBAC role, and supported API.
- [ ] Add a pre-event rehearsal checklist:
  registration, API key, Copilot App, Responses API, new chat, quota, metrics, and rollback.
- [ ] Add measured capacity profiles, including the current Copilot repository-session result:
  100K TPM supports 10 synchronized users; 600K TPM supports 50.
- [ ] Add operational guidance for alerts, quota monitoring, revision rollback, backup/restore, and
  event cleanup.

## Phase 3 — attendee journey

| Order | Page or surface | TL;DR content |
|---:|---|---|
| 1 | `attendee.md` | Open event URL, authenticate or use shared-code instructions, register, reveal/copy API key, copy endpoint and model. |
| 2 | Event registration page | Dynamic endpoint, key, models, Responses setting, token settings, screenshot-guide link, Python/C# examples. |
| 3 | `github_copilot_app.md` | Custom endpoint, exact Base URL, Responses, event key, model ID, 80000/4096 limits, save, start new chat. |
| 4 | SDK samples | Save downloaded config as `.env`, install dependencies, run one Python or C# file, confirm response. |
| 5 | Troubleshooting | Determine whether the failure is local context, authentication, route/API selection, quota, or stale chat configuration. |

### Attendee quickstart checklist

- [x] Add a minimum Copilot App TL;DR.
- [x] Link the event page to the screenshot walkthrough and single-file samples.
- [x] Add a TL;DR to attendee registration that covers both GitHub and shared-code paths.
- [ ] Add one expected-success screenshot from a clean Copilot chat.
- [ ] Put copy-paste run commands beside the downloaded `.env` and SDK sample links.
- [ ] Add a compact troubleshooting decision tree:
  - no proxy log → Copilot context/configuration;
  - 401 → expired or incorrect event key;
  - 404 → wrong endpoint or Wire API;
  - `rate_limit_exceeded`/429 → Azure model quota;
  - settings changed → start a new chat and reselect the model.
- [ ] Explain the difference between Copilot prompt/output limits, event token caps, and Azure TPM.
- [ ] Keep failed-chat cleanup guidance near validation and troubleshooting.

## Phase 4 — page-level TL;DR rollout

Add a TL;DR to each task-oriented page in this order:

1. [x] `deployment/azure.md`
2. [x] `resources.md`
3. [x] `events.md`
4. [x] `attendee.md`
5. [x] `capacity.md`
6. `20-service-installation/70-testing/20-load-testing.md`
7. `reporting.md`
8. `deployment/managed_identity.md`
9. `developers.md`
10. `faq.md` as a symptom-first index rather than a procedural guide

Pages that are conceptual rather than procedural, such as architecture and security, should use an
**At a glance** summary instead of a task TL;DR.

## Acceptance criteria

- An experienced administrator can go from clone to event-ready using only TL;DR sections and
  verification steps.
- An attendee can go from event URL to a successful Copilot response without reading administrator
  documentation.
- Every TL;DR uses current endpoint formats, model/API guidance, and commands.
- Every task page has one expected success result and one next-step link.
- No two pages give conflicting limits, prerequisites, authentication requirements, or URL formats.
- README links, MkDocs navigation, screenshots, and code sample links resolve.
- The public documentation deployment includes the same pages present in the repository.

## Definition of done for each page

- TL;DR added and limited to the minimum successful path.
- Detailed instructions remain available directly below it.
- Dynamic values identify their authoritative source.
- Validation and common failure modes are documented.
- Links to previous/next role steps are present.
- Commands and links are verified.
- Screenshots reflect the current UI and contain no secrets.

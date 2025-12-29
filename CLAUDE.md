# Claude Code Context for Bartleby

**GitHub Repository**: https://github.com/elephantgerald/bartleby

## Project Overview

Bartleby is an autonomous task orchestrator built with .NET 10 MAUI. It syncs work items from GitHub Issues, tracks dependencies in PlantUML, and uses Azure OpenAI to automatically work on tasks.

## Architecture

```
Bartleby.App (MAUI)          → UI layer, ViewModels, DI setup
    ↓
Bartleby.Services            → Orchestration, background services
    ↓
Bartleby.Infrastructure      → Implementations (GitHub, AI, LiteDB, PlantUML)
    ↓
Bartleby.Core                → Domain models, interfaces (no dependencies)
```

## Key Files

### Core Domain
- `src/Bartleby.Core/Models/WorkItem.cs` - Main work item entity
- `src/Bartleby.Core/Models/WorkItemStatus.cs` - Status enum (Pending, Ready, InProgress, Blocked, Complete, Failed)
- `src/Bartleby.Core/Interfaces/IWorkSource.cs` - Interface for external sources (GitHub, Jira)
- `src/Bartleby.Core/Interfaces/IAIProvider.cs` - Interface for AI execution
- `src/Bartleby.Core/Interfaces/IGraphStore.cs` - Interface for dependency graph

### Infrastructure
- `src/Bartleby.Infrastructure/Persistence/LiteDbContext.cs` - Database context
- `src/Bartleby.Infrastructure/WorkSources/StubWorkSource.cs` - Mock data (replace with GitHubWorkSource)
- `src/Bartleby.Infrastructure/AIProviders/StubAIProvider.cs` - Mock AI (replace with AzureOpenAIProvider)
- `src/Bartleby.Infrastructure/Graph/PlantUmlGraphStore.cs` - PlantUML read/write

### App
- `src/Bartleby.App/MauiProgram.cs` - DI registration
- `src/Bartleby.App/Views/` - XAML pages
- `src/Bartleby.App/ViewModels/` - MVVM ViewModels

## Build Commands

```bash
# Build entire solution
dotnet build

# Build and run Windows app
dotnet run --project src/Bartleby.App -f net10.0-windows10.0.19041.0

# Run tests
dotnet test
```

## Implementation Status

**Completed (MVP Complete!):**
- Solution structure with 4 projects
- Core domain models and interfaces
- LiteDB persistence layer
- Stub implementations for work source and AI
- MAUI app with Dashboard, Work Items, Blocked, Settings pages
- DI wired up
- Test infrastructure (xUnit, FluentAssertions, Moq)
- `PlantUmlParser` - Parse PlantUML to build dependency graph (59 tests)
- `DependencyResolver` - Determine which work items are ready (30 tests)
- `GitHubWorkSource` - GitHub integration using Octokit (42 tests)
- `SyncService` - Bidirectional sync between GitHub and local store (35 tests)
- `AzureOpenAIProvider` - Azure OpenAI integration with retry/rate limiting (19 tests)
- `WorkExecutor` - Prompt templates and transformation orchestration (38 tests)
- `OrchestratorService` - Background service with state machine, quiet hours, token budgets (46 tests)
- Blocked work management (Story #15)
- `GitService` - Auto-commit completed work with LibGit2Sharp (46 tests)

## Patterns & Conventions

- **MVVM**: Using CommunityToolkit.Mvvm with `[ObservableProperty]` and `[RelayCommand]`
- **DI**: All services registered in `MauiProgram.cs`
- **Async**: All I/O operations are async with CancellationToken support
- **Nullable**: Project uses nullable reference types

## Workflow for Completing Stories

**IMPORTANT:** Follow this order when completing work. Each step includes both GitHub Project board updates AND local cache updates.

### Step 1: Create Branch
Branch from `main` using ticket number + title in lowercase kebab-case:
- Format: `{issue-number}-{lowercase-kebab-title}`
- Example: `7-set-up-test-infrastructure`

### Step 2: Move Issue to "In Progress"
Update both the project board and local cache:

**Project Board:** Use the GraphQL mutation (see [Status Change Reference](#status-change-reference) below)

**Local Cache:** Update `.context/milestones/stories/story-{N}-*.md`:
- Change `**State:** Open` to `**State:** In Progress`

### Step 3: Implement
Write the code and tests.

### Step 4: Test
Run `dotnet test` and verify with the user.

### Step 5: Update Cached Docs (for completed state)
Update `.context/milestones/` files to reflect the completed state:
- **INDEX.md**: Update counts, change story status to `**Closed**`
- **Story file**: Change `**State:** In Progress` to `**State:** Closed`
- Mark all tasks/criteria as `[x]` completed
- Add implementation notes (files created, key details)

### Step 6: Commit & Push
Only after tests pass and user approves.

### Step 7: Create PR
Open a pull request for human review.

### Step 8: Move Issue to "In Review"
**Project Board:** Use the GraphQL mutation to change status to "In review"

### Step 9: After PR Merged - Move to "Done"
Close the GitHub issue (this may auto-move to Done, or use the mutation)

### Step 10: Unblock Dependent Stories
Check if completing this story unblocks others:
- Review stories in "Backlog" that depended on this one
- Move newly unblocked stories to "Ready" on project board
- Update local cache `.context/milestones/stories/story-{N}-*.md` for unblocked stories

---

## Status Change Reference

### Project IDs
| Name | ID |
|------|-----|
| Bartleby Kanban | `PVT_kwHOAFRux84BLb0B` |
| Status Field | `PVTSSF_lAHOAFRux84BLb0Bzg7Ammc` |

### Status Option IDs
| Status | Option ID |
|--------|-----------|
| Backlog | `f75ad846` |
| Ready | `61e4505c` |
| In progress | `47fc9ee4` |
| In review | `df73e18b` |
| Done | `98236657` |

### Get Item ID for an Issue
```bash
gh api graphql -f query='{
  repository(owner: "elephantgerald", name: "bartleby") {
    issue(number: ISSUE_NUMBER) {
      projectItems(first: 10) {
        nodes { id }
      }
    }
  }
}'
```

### Change Issue Status
Replace `ITEM_ID` with the item ID from above, and `OPTION_ID` with the desired status:
```bash
gh api graphql -f query='mutation {
  updateProjectV2ItemFieldValue(
    input: {
      projectId: "PVT_kwHOAFRux84BLb0B"
      itemId: "ITEM_ID"
      fieldId: "PVTSSF_lAHOAFRux84BLb0Bzg7Ammc"
      value: { singleSelectOptionId: "OPTION_ID" }
    }
  ) { projectV2Item { id } }
}'
```

---

## Rules
- Do NOT close GitHub issues until the PR is merged
- DO update cached docs (INDEX.md, story files) as part of the PR - they reflect the state *after* merge
- Always wait for user approval before committing
- Keep project board AND local cache in sync

## Why Update Docs Before Merge?
The cached docs in `.context/milestones/` track the state of the codebase. When the PR merges, the story IS complete, so the docs should reflect that. Including doc updates in the PR keeps everything in sync.

## MVP Complete!

All 10 stories across 5 milestones have been implemented. The Bartleby MVP is feature-complete with:
- PlantUML dependency parsing and resolution
- GitHub Issues synchronization
- Azure OpenAI work execution
- Background orchestration service
- Git auto-commit for completed work

## Important Notes

- The app currently uses **stub implementations** that return mock data
- Database is stored in `FileSystem.AppDataDirectory/Bartleby.db`
- PlantUML graph file is at `FileSystem.AppDataDirectory/dependencies.puml`
- Target frameworks: Windows (`net10.0-windows10.0.19041.0`) and macOS (`net10.0-maccatalyst`)

## Reference Documentation

| Document | Purpose | When to Use |
|----------|---------|-------------|
| [Design Document](.context/bartleby-design-doc.txt) | Vision, metaphors, UX philosophy | Consult when making UI/UX decisions, naming things, or ensuring changes align with Bartleby's identity as a "scrivener-agent." Key concepts: Registrar's Desk metaphor, Provenance/Parsimony/Canon principles, Ledger Drawer navigation. |
| [Implementation Plan](.context/bartleby-implementation-plan.md) | Architecture, phases, technical specs | Consult when implementing features, understanding project structure, or checking what's already built vs. what's next. Contains architecture diagrams, phase breakdowns, and NuGet package versions. |
| [Work Index](.context/milestones/INDEX.md) | Stories & milestones from GitHub | Consult when picking up work or understanding scope. Contains cached GitHub issues with full task lists, testing requirements, and acceptance criteria organized by milestone. |

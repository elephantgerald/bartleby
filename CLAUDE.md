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

# Run tests (when implemented)
dotnet test
```

## Implementation Status

**Completed:**
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

**Not Yet Implemented:**
- `AzureOpenAIProvider` - Real AI integration
- `OrchestratorService` - Background service to pick up and execute work
- `GitService` - Auto-commit completed work

## Patterns & Conventions

- **MVVM**: Using CommunityToolkit.Mvvm with `[ObservableProperty]` and `[RelayCommand]`
- **DI**: All services registered in `MauiProgram.cs`
- **Async**: All I/O operations are async with CancellationToken support
- **Nullable**: Project uses nullable reference types

## Workflow for Completing Stories

**IMPORTANT:** Follow this order when completing work:

1. **Create branch** - Branch from `main` using ticket number + title in lowercase kebab-case
   - Format: `{issue-number}-{lowercase-kebab-title}`
   - Example: `7-set-up-test-infrastructure`
2. **Move issue to In Progress** - Update the GitHub issue to indicate work has started
   - `gh issue edit {issue-number} --add-label "in-progress"`
   - This signals to others that the issue is actively being worked on
3. **Implement** - Write the code/tests
4. **Test together** - Run `dotnet test` and verify with the user
5. **Update cached docs** - Update `.context/milestones/` files to reflect completed state
   - Update `INDEX.md` counts and status (mark story as **Closed**)
   - Update the story file (`.context/milestones/stories/story-{N}-*.md`)
   - Mark all tasks/criteria as `[x]` completed
   - Add implementation notes (files created, key details)
   - Add PR link
6. **Commit & Push** - Only after tests pass and user approves
7. **Create PR** - Open a pull request for human review
8. **Move issue to In Review** - When the PR is ready for review
   - `gh issue edit {issue-number} --add-label "in-review" --remove-label "in-progress"`
   - This signals the code is complete and awaiting human review
9. **After PR merged** - Close the GitHub issue and remove `in-review` label
10. **Unblock dependent stories** - Check if completing this story unblocks others
    - Review stories in `backlog` status that depended on this one
    - Move newly unblocked stories to `ready` status: `gh issue edit {issue-number} --add-label "ready" --remove-label "backlog"`
    - This keeps the board current and signals what's available to work on next

**Rules:**
- Do NOT close GitHub issues until the PR is merged
- DO update cached docs (INDEX.md, story files) as part of the PR - they reflect the state *after* merge
- Always wait for user approval before committing

**Why update docs before merge?**
The cached docs in `.context/milestones/` track the state of the codebase. When the PR merges, the story IS complete, so the docs should reflect that. Including doc updates in the PR keeps everything in sync.

## Next Steps (Priority Order)

1. Implement `AzureOpenAIProvider` with Azure.AI.OpenAI SDK (Story #12)
2. Implement `WorkExecutor` with prompt templates (Story #13)
3. Implement `OrchestratorService` as a background service (Story #14)
4. Implement blocked work management (Story #15)
5. Implement `GitService` with LibGit2Sharp (Story #16)

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

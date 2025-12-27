# Claude Code Context for Bartleby

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

**Not Yet Implemented:**
- `OrchestratorService` - Background service to pick up and execute work
- `GitHubWorkSource` - Real GitHub integration using Octokit
- `AzureOpenAIProvider` - Real AI integration
- `PlantUmlParser` - Parse PlantUML to build dependency graph
- `GitService` - Auto-commit completed work

## Patterns & Conventions

- **MVVM**: Using CommunityToolkit.Mvvm with `[ObservableProperty]` and `[RelayCommand]`
- **DI**: All services registered in `MauiProgram.cs`
- **Async**: All I/O operations are async with CancellationToken support
- **Nullable**: Project uses nullable reference types

## Next Steps (Priority Order)

1. Implement `OrchestratorService` as a background service
2. Implement `GitHubWorkSource` with Octokit
3. Implement `AzureOpenAIProvider` with Azure.AI.OpenAI SDK
4. Add PlantUML parsing to `PlantUmlGraphStore`
5. Implement `GitService` with LibGit2Sharp

## Important Notes

- The app currently uses **stub implementations** that return mock data
- Database is stored in `FileSystem.AppDataDirectory/Bartleby.db`
- PlantUML graph file is at `FileSystem.AppDataDirectory/dependencies.puml`
- Target frameworks: Windows (`net10.0-windows10.0.19041.0`) and macOS (`net10.0-maccatalyst`)

## Reference Documentation

- Full implementation plan: `.context/bartleby-implementation-plan.md`

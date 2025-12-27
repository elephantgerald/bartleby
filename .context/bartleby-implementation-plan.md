# Bartleby - Autonomous Task Orchestrator

## Summary
A .NET MAUI application that syncs work items from external sources (GitHub Issues), tracks dependencies in PlantUML, and uses AI (Azure OpenAI) to autonomously work on tasks.

## Key Decisions
- **UI**: .NET MAUI (Windows + macOS)
- **Storage**: LiteDB (document database - better fit for varying work item shapes)
- **AI Provider**: Azure OpenAI (first), pluggable interface for future providers
- **Work Source**: GitHub Issues (first), pluggable interface for future sources
- **Graph**: PlantUML as bidirectional (source of truth + visualization)
- **Execution**: Background service, continuous autonomous operation

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                      MAUI UI Layer                          │
│  (Dashboard, Work Items, Graph View, Settings, Blocked Q&A) │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                    Application Services                      │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────────────┐ │
│  │ Orchestrator │ │ Sync Service │ │ Dependency Resolver  │ │
│  │   Service    │ │              │ │                      │ │
│  └──────────────┘ └──────────────┘ └──────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                     Core Abstractions                        │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────────────┐ │
│  │ IWorkSource  │ │ IAIProvider  │ │ IGraphStore          │ │
│  │ (GitHub)     │ │ (AzureOpenAI)│ │ (PlantUML)           │ │
│  └──────────────┘ └──────────────┘ └──────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                      Data Layer                              │
│              LiteDB (Work Items, Settings, Logs)             │
└─────────────────────────────────────────────────────────────┘
```

## Project Structure

```
Bartleby/
├── Bartleby.sln
├── src/
│   ├── Bartleby.App/                    # MAUI Application
│   │   ├── Bartleby.App.csproj
│   │   ├── MauiProgram.cs
│   │   ├── App.xaml(.cs)
│   │   ├── AppShell.xaml(.cs)
│   │   ├── Views/
│   │   │   ├── DashboardPage.xaml(.cs)
│   │   │   ├── WorkItemsPage.xaml(.cs)
│   │   │   ├── GraphPage.xaml(.cs)
│   │   │   ├── BlockedPage.xaml(.cs)
│   │   │   └── SettingsPage.xaml(.cs)
│   │   ├── ViewModels/
│   │   │   ├── DashboardViewModel.cs
│   │   │   ├── WorkItemsViewModel.cs
│   │   │   ├── GraphViewModel.cs
│   │   │   ├── BlockedViewModel.cs
│   │   │   └── SettingsViewModel.cs
│   │   └── Resources/
│   │
│   ├── Bartleby.Core/                   # Domain Models & Interfaces
│   │   ├── Bartleby.Core.csproj
│   │   ├── Models/
│   │   │   ├── WorkItem.cs
│   │   │   ├── WorkItemStatus.cs
│   │   │   ├── BlockedQuestion.cs
│   │   │   ├── WorkSession.cs
│   │   │   └── DependencyGraph.cs
│   │   ├── Interfaces/
│   │   │   ├── IWorkSource.cs
│   │   │   ├── IAIProvider.cs
│   │   │   ├── IGraphStore.cs
│   │   │   └── IWorkItemRepository.cs
│   │   └── Events/
│   │       └── WorkItemEvents.cs
│   │
│   ├── Bartleby.Infrastructure/         # Implementations
│   │   ├── Bartleby.Infrastructure.csproj
│   │   ├── WorkSources/
│   │   │   └── GitHubWorkSource.cs
│   │   ├── AIProviders/
│   │   │   └── AzureOpenAIProvider.cs
│   │   ├── Graph/
│   │   │   ├── PlantUmlGraphStore.cs
│   │   │   └── PlantUmlParser.cs
│   │   ├── Persistence/
│   │   │   ├── LiteDbContext.cs
│   │   │   └── WorkItemRepository.cs
│   │   └── Git/
│   │       └── GitService.cs
│   │
│   └── Bartleby.Services/               # Application Services
│       ├── Bartleby.Services.csproj
│       ├── OrchestratorService.cs       # Main background service
│       ├── SyncService.cs               # Syncs external sources
│       ├── DependencyResolver.cs        # Determines what can be worked
│       └── WorkExecutor.cs              # Runs AI on work items
│
└── tests/
    ├── Bartleby.Core.Tests/
    ├── Bartleby.Infrastructure.Tests/
    └── Bartleby.Services.Tests/
```

## Implementation Phases

### Phase 1: Foundation (Start Here)
1. Create solution and project structure
2. Define core domain models (`WorkItem`, `WorkItemStatus`, `DependencyGraph`)
3. Define core interfaces (`IWorkSource`, `IAIProvider`, `IGraphStore`)
4. Set up LiteDB persistence layer
5. Create minimal MAUI shell with navigation

### Phase 2: PlantUML Graph
1. Implement `PlantUmlParser` - parse PlantUML to dependency graph
2. Implement `PlantUmlGraphStore` - read/write PlantUML files
3. Create graph visualization in MAUI (could start with WebView + PlantUML server)
4. Build `DependencyResolver` - find workable items (no unmet dependencies)

### Phase 3: GitHub Integration
1. Implement `GitHubWorkSource` using Octokit
2. Build `SyncService` - periodic sync from GitHub Issues
3. Map GitHub Issues to local `WorkItem` model
4. Two-way sync: local status changes → GitHub labels/status

### Phase 4: Azure OpenAI Integration
1. Implement `AzureOpenAIProvider` using Azure.AI.OpenAI SDK
2. Design prompt templates for task execution
3. Build `WorkExecutor` - sends work to AI, captures output
4. Handle AI responses: code changes, questions, completion

### Phase 5: Orchestrator Service
1. Implement `OrchestratorService` as background service
2. State machine: Ready → InProgress → Blocked/Complete
3. Question generation when AI gets stuck
4. Blocked work management and answer handling

### Phase 6: Git Integration
1. Implement `GitService` using LibGit2Sharp
2. Auto-commit on work completion
3. Branch management per work item
4. Commit message generation from work context

### Phase 7: Polish & UI
1. Dashboard with status overview
2. Work items list with filtering
3. Blocked items Q&A interface
4. Settings management (API keys, repos, etc.)

## Key NuGet Packages

```xml
<!-- Core -->
<PackageReference Include="LiteDB" Version="5.*" />

<!-- MAUI -->
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
<PackageReference Include="CommunityToolkit.Maui" Version="7.*" />

<!-- GitHub -->
<PackageReference Include="Octokit" Version="9.*" />

<!-- Azure OpenAI -->
<PackageReference Include="Azure.AI.OpenAI" Version="1.*" />

<!-- Git -->
<PackageReference Include="LibGit2Sharp" Version="0.30.*" />
```

## Work Item Lifecycle

```
┌─────────┐     Sync      ┌─────────┐
│ External│──────────────▶│  Ready  │
│ (GitHub)│               └────┬────┘
└─────────┘                    │
                               │ Dependencies met?
                               ▼
                         ┌───────────┐
                         │InProgress │◀──────────────┐
                         └─────┬─────┘               │
                               │                     │
              ┌────────────────┼────────────────┐    │
              ▼                ▼                ▼    │
        ┌──────────┐    ┌───────────┐    ┌─────────┐│
        │ Complete │    │  Blocked  │    │ Failed  ││
        └────┬─────┘    └─────┬─────┘    └─────────┘│
             │                │                      │
             ▼                │ Answers provided     │
        Git Commit            └──────────────────────┘
        Update External
```

## MVP Implementation Plan

### Step 1: Solution & Project Structure
Create the solution with all projects, proper references, and NuGet packages.

**Files to create:**
- `Bartleby.sln`
- `src/Bartleby.Core/Bartleby.Core.csproj`
- `src/Bartleby.Infrastructure/Bartleby.Infrastructure.csproj`
- `src/Bartleby.Services/Bartleby.Services.csproj`
- `src/Bartleby.App/Bartleby.App.csproj`

### Step 2: Core Domain Models & Interfaces
Define the domain without any implementations.

**Files to create:**
- `WorkItem.cs` - Id, Title, Description, Status, ExternalId, Source, Dependencies
- `WorkItemStatus.cs` - Enum: Ready, InProgress, Blocked, Complete, Failed
- `BlockedQuestion.cs` - Question text, context, WorkItemId
- `IWorkSource.cs` - SyncAsync(), UpdateStatusAsync()
- `IAIProvider.cs` - ExecuteWorkAsync(), GenerateQuestionsAsync()
- `IGraphStore.cs` - LoadGraphAsync(), SaveGraphAsync(), AddDependency()
- `IWorkItemRepository.cs` - CRUD for work items

### Step 3: Stub Implementations
Create placeholder implementations that return mock data.

**Files to create:**
- `StubWorkSource.cs` - Returns 3 fake work items
- `StubAIProvider.cs` - Simulates work with delays
- `PlantUmlGraphStore.cs` - Basic file read/write (no parsing yet)
- `LiteDbContext.cs` + `WorkItemRepository.cs` - Real LiteDB (simple enough)

### Step 4: MAUI App Shell
Create the app structure with navigation.

**Files to create:**
- `MauiProgram.cs` - DI registration
- `App.xaml` + `AppShell.xaml` - Tab navigation
- `DashboardPage.xaml` - Orchestrator status, quick stats
- `WorkItemsPage.xaml` - List of work items with status
- `SettingsPage.xaml` - API keys, repo config

### Step 5: Background Orchestrator
Wire up the background service (stub logic initially).

**Files to create:**
- `OrchestratorService.cs` - Timer-based background service
- `WorkExecutor.cs` - Calls AI provider, handles results

### Step 6: Replace Stubs (Iterate)
Replace stubs one at a time:
1. GitHub integration (Octokit)
2. Azure OpenAI integration
3. PlantUML parsing
4. Git commit integration

---

## Current Status (Updated)

### Completed (Phase 1 - Foundation)
- [x] Solution and project structure created
- [x] Core domain models: `WorkItem`, `WorkItemStatus`, `BlockedQuestion`, `WorkSession`, `DependencyGraph`, `AppSettings`
- [x] Core interfaces: `IWorkSource`, `IAIProvider`, `IGraphStore`, `IWorkItemRepository`, `IBlockedQuestionRepository`, `IWorkSessionRepository`, `ISettingsRepository`
- [x] Stub implementations: `StubWorkSource`, `StubAIProvider`
- [x] LiteDB persistence: `LiteDbContext`, `WorkItemRepository`, `BlockedQuestionRepository`, `WorkSessionRepository`, `SettingsRepository`
- [x] PlantUML graph store (basic read/write, no parsing yet)
- [x] MAUI app shell with tab navigation (Dashboard, Work Items, Blocked, Settings)
- [x] ViewModels with MVVM Toolkit
- [x] Value converters for XAML bindings
- [x] Dependency injection wired up in `MauiProgram.cs`
- [x] App builds and runs on Windows

### Not Yet Implemented
- [ ] GraphPage/GraphViewModel (skipped for MVP - raw PlantUML text only)
- [ ] Events/WorkItemEvents.cs
- [ ] PlantUmlParser.cs (full parsing)
- [ ] OrchestratorService.cs (background service)
- [ ] SyncService.cs
- [ ] DependencyResolver.cs
- [ ] WorkExecutor.cs
- [ ] GitHubWorkSource.cs (real GitHub integration)
- [ ] AzureOpenAIProvider.cs (real AI integration)
- [ ] GitService.cs
- [ ] Test projects

### Next Steps
1. **Phase 2**: Implement PlantUML parsing and DependencyResolver
2. **Phase 3**: Replace StubWorkSource with GitHubWorkSource (Octokit)
3. **Phase 4**: Replace StubAIProvider with AzureOpenAIProvider
4. **Phase 5**: Implement OrchestratorService background service
5. **Phase 6**: Add GitService for auto-commits

### How to Run
```bash
cd src/Bartleby.App
dotnet run -f net10.0-windows10.0.19041.0
```

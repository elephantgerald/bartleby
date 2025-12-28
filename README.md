# Bartleby

An autonomous task orchestrator that syncs work items from external sources, tracks dependencies, and uses AI to automatically work on tasks.

## Overview

Bartleby connects to your issue trackers (GitHub Issues), caches work items locally, tracks their dependencies in a PlantUML graph, and uses Azure OpenAI to autonomously pick up and complete work. When the AI gets stuck, it generates questions and moves the work to a "blocked" state until you provide answers.

## Features

- **Work Item Sync**: Pull issues from GitHub (more sources planned)
- **Dependency Tracking**: PlantUML-based dependency graph
- **AI Execution**: Azure OpenAI works on tasks autonomously
- **Blocked Queue**: AI generates questions when stuck; you answer, it resumes
- **Auto-Commit**: Completed work is committed to git automatically
- **Local-First**: LiteDB storage for offline capability

## Tech Stack

- **.NET 10** / **MAUI** (Windows + macOS)
- **LiteDB** for local document storage
- **Azure OpenAI** for AI task execution
- **Octokit** for GitHub integration
- **PlantUML** for dependency visualization
- **CommunityToolkit.Mvvm** for MVVM pattern

## Project Structure

```
bartleby/
├── src/
│   ├── Bartleby.Core/           # Domain models & interfaces
│   ├── Bartleby.Infrastructure/ # Implementations (GitHub, AI, DB)
│   ├── Bartleby.Services/       # Orchestration & business logic
│   └── Bartleby.App/            # MAUI application
├── tests/                       # Unit & integration tests
└── .context/                    # Project documentation
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- MAUI workload: `dotnet workload install maui`

### Build & Run

```bash
# Clone and build
git clone https://github.com/elephantgerald/bartleby.git
cd bartleby
dotnet build

# Run the app (Windows)
cd src/Bartleby.App
dotnet run -f net10.0-windows10.0.19041.0
```

### Configuration

On first run, go to **Settings** and configure:

1. **Azure OpenAI**: Endpoint, API Key, Deployment Name
2. **GitHub**: Personal Access Token, Owner, Repository

## How It Works

```
┌─────────┐     Sync      ┌─────────┐
│ GitHub  │──────────────▶│  Ready  │
│ Issues  │               └────┬────┘
└─────────┘                    │
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
             ▼                │ Answer provided      │
        Git Commit            └──────────────────────┘
```

1. **Sync**: Work items are pulled from GitHub Issues
2. **Ready**: Items with all dependencies met are ready for AI
3. **In Progress**: AI is actively working on the item
4. **Blocked**: AI needs answers to proceed
5. **Complete**: Work done, changes committed to git

## Current Status

**Phase 1 Complete** - Foundation skeleton is built and running.

## Development Workflow

When working on stories/issues, follow this process:

1. **Create a branch** from `main` using lowercase kebab-case:
   ```bash
   git checkout -b {issue-number}-{title-in-kebab-case}
   # Example: git checkout -b 7-set-up-test-infrastructure
   ```

2. **Implement** the feature/fix

3. **Test** before committing:
   ```bash
   dotnet test
   ```

4. **Update cached docs** (include in the PR):
   - Update `.context/milestones/INDEX.md` (counts, status)
   - Update `.context/milestones/stories/story-{N}-*.md` (mark tasks complete, add notes)
   - These reflect the state *after* merge, so update them before creating the PR

5. **Commit & Push** your changes

6. **Open a PR** for human review

7. **After merge**, close the GitHub issue

## Documentation

| Document | Purpose | When to Read |
|----------|---------|--------------|
| [Design Document](.context/bartleby-design-doc.txt) | Vision, metaphors, UX philosophy | Understanding *why* Bartleby works the way it does. Read this to grasp the "Registrar's Desk" narrative, the guiding principles (Provenance, Parsimony, Canon), and the intended user experience. |
| [Implementation Plan](.context/bartleby-implementation-plan.md) | Architecture, phases, technical specs | Understanding *how* to build Bartleby. Read this for project structure, implementation phases, NuGet packages, and current development status. |
| [Work Index](.context/milestones/INDEX.md) | Stories & milestones from GitHub | Tracking *what* needs to be done. Browse cached GitHub issues organized by milestone with task lists and acceptance criteria. |

## License

MIT

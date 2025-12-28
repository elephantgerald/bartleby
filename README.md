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

**Phase 3 Complete** - GitHub Integration is built and tested.

## Development Workflow

When working on stories/issues, follow this process. Status changes update both the **GitHub Project board** (via GraphQL API) and **local cache** (`.context/milestones/`).

1. **Create a branch** from `main` using lowercase kebab-case:
   ```bash
   git checkout -b {issue-number}-{title-in-kebab-case}
   # Example: git checkout -b 7-set-up-test-infrastructure
   ```

2. **Move issue to "In progress"** on the project board and update local cache:
   - Update `.context/milestones/stories/story-{N}-*.md`: Change `**State:** Open` to `**State:** In Progress`

3. **Implement** the feature/fix

4. **Test** before committing:
   ```bash
   dotnet test
   ```

5. **Update cached docs** (include in the PR):
   - Update `.context/milestones/INDEX.md` (counts, change status to `**Closed**`)
   - Update `.context/milestones/stories/story-{N}-*.md` (mark tasks complete, add notes)
   - These reflect the state *after* merge, so update them before creating the PR

6. **Commit & Push** your changes

7. **Open a PR** for human review

8. **Move issue to "In review"** on the project board

9. **After merge**, close the GitHub issue (moves to "Done")

10. **Unblock dependent stories** - Check if completing this story unblocks others:
    - Move newly unblocked stories from "Backlog" to "Ready" on project board
    - Update their local cache files accordingly

See [CLAUDE.md](./CLAUDE.md) for detailed GraphQL commands for status changes.

## Documentation

| Document | Purpose | When to Read |
|----------|---------|--------------|
| [Design Document](.context/bartleby-design-doc.txt) | Vision, metaphors, UX philosophy | Understanding *why* Bartleby works the way it does. Read this to grasp the "Registrar's Desk" narrative, the guiding principles (Provenance, Parsimony, Canon), and the intended user experience. |
| [Implementation Plan](.context/bartleby-implementation-plan.md) | Architecture, phases, technical specs | Understanding *how* to build Bartleby. Read this for project structure, implementation phases, NuGet packages, and current development status. |
| [Work Index](.context/milestones/INDEX.md) | Stories & milestones from GitHub | Tracking *what* needs to be done. Browse cached GitHub issues organized by milestone with task lists and acceptance criteria. |

## License

MIT

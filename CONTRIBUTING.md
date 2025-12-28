# Contributing to Bartleby

## Development Environment Setup

### Required Tools

#### 1. .NET 10 SDK

Download and install from [dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0)

Or via WinGet (Windows):
```powershell
winget install Microsoft.DotNet.SDK.10
```

Verify installation:
```bash
dotnet --version
# Should show 10.0.x
```

#### 2. MAUI Workload

Install the MAUI workload for cross-platform UI development:
```bash
dotnet workload install maui
```

Verify installation:
```bash
dotnet workload list
# Should show 'maui' in the list
```

#### 3. IDE (Pick One)

**Visual Studio 2022** (Windows)
- Install with ".NET Multi-platform App UI development" workload
- Note: VS 2022 has limited .NET 10 support; some features may require VS 2026

**Visual Studio Code**
- Install [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) extension
- Install [.NET MAUI](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.dotnet-maui) extension

**JetBrains Rider**
- Full .NET 10 and MAUI support out of the box

### Recommended Tools

#### Claude Code CLI

This project was built with Claude Code and includes a `CLAUDE.md` context file. Having Claude Code installed makes it easy to continue development with full project context.

```bash
# Install via npm
npm install -g @anthropic-ai/claude-code

# Or via Homebrew (macOS)
brew install claude-code
```

See [claude.ai/claude-code](https://claude.ai/claude-code) for setup instructions.

#### Git

Standard Git installation. The project will eventually include auto-commit features via LibGit2Sharp.

---

## Building the Project

### Clone and Restore

```bash
git clone https://github.com/elephantgerald/bartleby.git
cd bartleby
dotnet restore
```

### Build

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/Bartleby.App
```

### Run

```bash
# Windows
dotnet run --project src/Bartleby.App -f net10.0-windows10.0.19041.0

# macOS (from a Mac)
dotnet run --project src/Bartleby.App -f net10.0-maccatalyst
```

### Run Tests

```bash
dotnet test
```

---

## Project Structure

```
bartleby/
├── src/
│   ├── Bartleby.Core/           # Domain models & interfaces (no external deps)
│   ├── Bartleby.Infrastructure/ # Implementations (DB, APIs, file I/O)
│   ├── Bartleby.Services/       # Business logic, orchestration
│   └── Bartleby.App/            # MAUI UI application
├── tests/
│   ├── Bartleby.Core.Tests/
│   ├── Bartleby.Infrastructure.Tests/
│   └── Bartleby.Services.Tests/
├── .context/                    # Project documentation & plans
├── CLAUDE.md                    # Context for Claude Code
└── README.md
```

---

## Code Style & Conventions

### General
- Use C# 14 features where appropriate
- Nullable reference types are enabled (`<Nullable>enable</Nullable>`)
- Implicit usings are enabled

### Naming
- `PascalCase` for public members, types, methods
- `_camelCase` for private fields
- `camelCase` for local variables and parameters

### Architecture
- **Core**: Pure domain logic, no external dependencies
- **Infrastructure**: All I/O (database, HTTP, file system)
- **Services**: Orchestration, business rules, background services
- **App**: UI only, thin ViewModels that delegate to services

### MVVM Pattern
We use CommunityToolkit.Mvvm source generators:

```csharp
public partial class MyViewModel : ObservableObject
{
    [ObservableProperty]
    private string _myProperty = string.Empty;

    [RelayCommand]
    private async Task DoSomethingAsync()
    {
        // ...
    }
}
```

### Async/Await
- All I/O operations should be async
- Include `CancellationToken` parameter on async methods
- Use `ConfigureAwait(false)` in library code (Core, Infrastructure, Services)

### Dependency Injection
- Register services in `MauiProgram.cs`
- Use constructor injection
- Prefer interfaces over concrete types

---

## Making Changes

### Before You Start
1. Check `.context/bartleby-implementation-plan.md` for current status and roadmap
2. Look at existing code patterns in similar files
3. If using Claude Code, it will automatically read `CLAUDE.md` for context

### Workflow
1. Create a feature branch: `git checkout -b feature/my-feature`
2. Make your changes
3. Ensure it builds: `dotnet build`
4. Run tests: `dotnet test`
5. Submit a pull request

### Adding New Features

**New Model**: Add to `Bartleby.Core/Models/`

**New Interface**: Add to `Bartleby.Core/Interfaces/`

**New Implementation**: Add to appropriate `Bartleby.Infrastructure/` subfolder

**New Service**: Add to `Bartleby.Services/`

**New Page**:
1. Create ViewModel in `Bartleby.App/ViewModels/`
2. Create XAML page in `Bartleby.App/Views/`
3. Register both in `MauiProgram.cs`
4. Add to `AppShell.xaml` navigation

---

## Current Development Priorities

See `.context/bartleby-implementation-plan.md` for the full roadmap. Current priorities:

1. **OrchestratorService** - Background service for autonomous work execution
2. **GitHubWorkSource** - Replace stub with real GitHub API (Octokit)
3. **AzureOpenAIProvider** - Replace stub with real Azure OpenAI
4. **PlantUML Parser** - Parse PlantUML files into dependency graph
5. **GitService** - Auto-commit completed work

---

## Getting Help

- Check `CLAUDE.md` for architecture overview
- Check `.context/bartleby-implementation-plan.md` for detailed plans
- Open an issue for questions or bugs

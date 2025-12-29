# Story #16: Implement GitService with LibGit2Sharp

**GitHub URL:** https://github.com/elephantgerald/bartleby/issues/16
**State:** Closed
**Labels:** `story`, `phase-6`
**Milestone:** [Phase 6: Git Integration](../milestone-5-git-integration.md)

## Overview

Automate git operations for completed work using LibGit2Sharp.

## Tasks

- [x] Add LibGit2Sharp NuGet package
- [x] Create `GitService.cs` in `Bartleby.Infrastructure/Git/`
- [x] Implement repository detection/initialization
- [x] Create branch per work item (naming convention: `bartleby/{work-item-id}`)
- [x] Stage and commit changes on work completion
- [x] Generate commit messages from work context
- [x] Handle merge conflicts gracefully
- [x] Optional: push to remote

## Testing Requirements

- [x] Unit tests with mock git operations (32 tests)
- [x] Integration tests with real git repository (14 tests)
- [x] Tests for branch creation
- [x] Tests for commit message generation
- [x] Tests for conflict scenarios

## Acceptance Criteria

- [x] Completed work creates a commit
- [x] Commits are on dedicated branches
- [x] Commit messages are descriptive
- [x] Conflicts don't crash the service
- [x] All tests pass

## Implementation Notes

### Files Created
- `src/Bartleby.Core/Interfaces/IGitService.cs` - Interface defining git operations
- `src/Bartleby.Core/Models/GitModels.cs` - GitOperationResult and GitRepositoryStatus models
- `src/Bartleby.Infrastructure/Git/IRepositoryWrapper.cs` - Wrapper for LibGit2Sharp (testability)
- `src/Bartleby.Infrastructure/Git/GitService.cs` - Main implementation
- `tests/Bartleby.Infrastructure.Tests/Git/GitServiceTests.cs` - Unit tests (32 tests)
- `tests/Bartleby.Infrastructure.Tests/Git/GitServiceIntegrationTests.cs` - Integration tests (14 tests)

### Key Features
- Branch naming: `bartleby/{external-id}-{sanitized-title}` or `bartleby/{guid-prefix}-{sanitized-title}`
- Commit type detection from labels (bug, feature, docs, test, refactor) or title keywords
- Commit messages follow conventional commit format (max 72 char first line)
- Includes work item summary, external URL reference, and modified files list
- Graceful conflict detection (returns ConflictingFiles list instead of crashing)
- Push support with authentication error handling

### DI Registration
- Registered as `IGitService` -> `GitService` singleton in `MauiProgram.cs`

---
**Story**: Testable unit of code | **Parent Epic**: #6 Git Integration

---
*Cached from GitHub: 2025-12-29*

# Story #16: Implement GitService with LibGit2Sharp

**GitHub URL:** https://github.com/elephantgerald/bartleby/issues/16
**State:** Open
**Labels:** `story`, `phase-6`
**Milestone:** [Phase 6: Git Integration](../milestone-5-git-integration.md)

## Overview

Automate git operations for completed work using LibGit2Sharp.

## Tasks

- [ ] Add LibGit2Sharp NuGet package
- [ ] Create `GitService.cs` in `Bartleby.Infrastructure/Git/`
- [ ] Implement repository detection/initialization
- [ ] Create branch per work item (naming convention: `bartleby/{work-item-id}`)
- [ ] Stage and commit changes on work completion
- [ ] Generate commit messages from work context
- [ ] Handle merge conflicts gracefully
- [ ] Optional: push to remote

## Testing Requirements

- [ ] Unit tests with mock git operations
- [ ] Integration tests with real git repository
- [ ] Tests for branch creation
- [ ] Tests for commit message generation
- [ ] Tests for conflict scenarios

## Acceptance Criteria

- [ ] Completed work creates a commit
- [ ] Commits are on dedicated branches
- [ ] Commit messages are descriptive
- [ ] Conflicts don't crash the service
- [ ] All tests pass

---
**Story**: Testable unit of code | **Parent Epic**: #6 Git Integration

---
*Cached from GitHub: 2025-12-28*

# Story #10: Implement GitHubWorkSource with Octokit

**GitHub URL:** https://github.com/elephantgerald/bartleby/issues/10
**State:** Open
**Labels:** `story`, `phase-3`
**Milestone:** [Phase 3: GitHub Integration](../milestone-2-github-integration.md)

## Overview

Replace `StubWorkSource` with a real GitHub implementation using Octokit.

## Tasks

- [ ] Add Octokit NuGet package
- [ ] Create `GitHubWorkSource.cs` in `Bartleby.Infrastructure/WorkSources/`
- [ ] Implement authentication (PAT or GitHub App)
- [ ] Fetch issues from configured repository
- [ ] Map GitHub Issue fields to `WorkItem` model
- [ ] Handle pagination for large issue lists
- [ ] Implement `UpdateStatusAsync` to update GitHub issue state/labels

## Testing Requirements

- [ ] Unit tests with mocked Octokit client
- [ ] Integration tests with real GitHub API (optional, requires PAT)
- [ ] Tests for authentication failure handling
- [ ] Tests for pagination
- [ ] Tests for mapping edge cases (missing fields, special characters)

## Acceptance Criteria

- [ ] Can fetch issues from any public/accessible repository
- [ ] Issue data correctly maps to WorkItem model
- [ ] Can update issue status back to GitHub
- [ ] All tests pass

---
**Story**: Testable unit of code | **Parent Epic**: #3 GitHub Integration

---
*Cached from GitHub: 2025-12-28*

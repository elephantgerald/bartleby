# Story #10: Implement GitHubWorkSource with Octokit

**GitHub URL:** https://github.com/elephantgerald/bartleby/issues/10
**State:** Closed
**Labels:** `story`, `phase-3`
**Milestone:** [Phase 3: GitHub Integration](../milestone-2-github-integration.md)
**PR:** [#26](https://github.com/elephantgerald/bartleby/pull/26)

## Overview

Replace `StubWorkSource` with a real GitHub implementation using Octokit.

## Tasks

- [x] Add Octokit NuGet package
- [x] Create `GitHubWorkSource.cs` in `Bartleby.Infrastructure/WorkSources/`
- [x] Implement authentication (PAT or GitHub App)
- [x] Fetch issues from configured repository
- [x] Map GitHub Issue fields to `WorkItem` model
- [x] Handle pagination for large issue lists
- [x] Implement `UpdateStatusAsync` to update GitHub issue state/labels

## Testing Requirements

- [x] Unit tests with mocked Octokit client
- [ ] Integration tests with real GitHub API (optional, requires PAT)
- [x] Tests for authentication failure handling
- [x] Tests for pagination
- [x] Tests for mapping edge cases (missing fields, special characters)

## Acceptance Criteria

- [x] Can fetch issues from any public/accessible repository
- [x] Issue data correctly maps to WorkItem model
- [x] Can update issue status back to GitHub
- [x] All tests pass

---

## Implementation Notes

### Files Created
- `src/Bartleby.Infrastructure/WorkSources/GitHubWorkSource.cs` - Main implementation of `IWorkSource`
- `src/Bartleby.Infrastructure/WorkSources/IGitHubApiClient.cs` - Abstraction interface and DTOs for testability
- `src/Bartleby.Infrastructure/WorkSources/OctokitGitHubApiClient.cs` - Octokit-based implementation of the API client
- `tests/Bartleby.Infrastructure.Tests/WorkSources/GitHubWorkSourceTests.cs` - 41 unit tests

### Key Design Decisions

1. **Wrapper Interface Pattern**: Created `IGitHubApiClient` interface to wrap Octokit's client. This allows:
   - Easy mocking in unit tests without dealing with Octokit's sealed/non-virtual types
   - Clean separation between our code and the Octokit library
   - Simple DTOs (`GitHubIssue`, `GitHubIssueUpdate`) that are easy to work with

2. **Client Factory Pattern**: `GitHubWorkSource` accepts a factory function for creating the API client, enabling dependency injection for testing.

3. **Consistent GUID Generation**: Uses MD5 hash of `"GitHub:{issueNumber}"` to generate deterministic GUIDs for work items, ensuring the same issue always maps to the same WorkItem ID.

4. **Label-Based Status Mapping**: Maps GitHub labels to `WorkItemStatus`:
   - `bartleby:ready` or `ready` → Ready
   - `bartleby:in-progress` or `in progress` → InProgress
   - `bartleby:blocked` or `blocked` → Blocked
   - `bartleby:failed` or `failed` → Failed
   - No status labels → Pending

5. **Pagination Support**: The Octokit wrapper handles pagination internally, fetching all issues across multiple pages.

### Test Coverage (41 tests)
- Name property tests
- SyncAsync tests (empty results, mapping, filtering PRs, pagination, consistent GUIDs)
- Label to status mapping tests (all status types, mixed case, defaults)
- UpdateStatusAsync tests (validation, closing issues, label updates)
- AddCommentAsync tests (validation, comment creation)
- TestConnectionAsync tests (configuration validation, success/failure handling)
- Constructor tests (null argument validation)
- Edge case tests (null fields, special characters, unicode)

---
**Story**: Testable unit of code | **Parent Epic**: #3 GitHub Integration

---
*Cached from GitHub: 2025-12-28*

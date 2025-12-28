# Story #11: Implement SyncService for bidirectional sync

**GitHub URL:** https://github.com/elephantgerald/bartleby/issues/11
**State:** Open
**Labels:** `story`, `phase-3`
**Milestone:** [Phase 3: GitHub Integration](../milestone-2-github-integration.md)

## Overview

Orchestrate periodic synchronization between GitHub and local work item store.

## Tasks

- [ ] Create `SyncService.cs` in `Bartleby.Services/`
- [ ] Implement sync from GitHub → local store
- [ ] Implement sync from local store → GitHub
- [ ] Handle conflict resolution (which source wins?)
- [ ] Track sync timestamps to detect changes
- [ ] Support manual trigger and scheduled sync
- [ ] Emit sync events for UI updates

## Testing Requirements

- [ ] Unit tests for new items being added locally
- [ ] Unit tests for existing items being updated
- [ ] Unit tests for items deleted from source
- [ ] Unit tests for conflict resolution scenarios
- [ ] Unit tests for sync scheduling logic

## Acceptance Criteria

- [ ] New GitHub issues appear in local store after sync
- [ ] Local status changes sync back to GitHub
- [ ] Conflicts are handled according to defined strategy
- [ ] All tests pass

---
**Story**: Testable unit of code | **Parent Epic**: #3 GitHub Integration

---
*Cached from GitHub: 2025-12-28*

# Story #11: Implement SyncService for bidirectional sync

**GitHub URL:** https://github.com/elephantgerald/bartleby/issues/11
**State:** Closed
**Labels:** `story`, `phase-3`
**Milestone:** [Phase 3: GitHub Integration](../milestone-2-github-integration.md)

## Overview

Orchestrate periodic synchronization between GitHub and local work item store.

## Tasks

- [x] Create `SyncService.cs` in `Bartleby.Services/`
- [x] Implement sync from GitHub -> local store
- [x] Implement sync from local store -> GitHub
- [x] Handle conflict resolution (which source wins?)
- [x] Track sync timestamps to detect changes
- [x] Support manual trigger and scheduled sync
- [x] Emit sync events for UI updates

## Testing Requirements

- [x] Unit tests for new items being added locally
- [x] Unit tests for existing items being updated
- [x] Unit tests for items deleted from source
- [x] Unit tests for conflict resolution scenarios
- [x] Unit tests for sync scheduling logic

## Acceptance Criteria

- [x] New GitHub issues appear in local store after sync
- [x] Local status changes sync back to GitHub
- [x] Conflicts are handled according to defined strategy
- [x] All tests pass

---

## Implementation Notes

### Files Created

- `src/Bartleby.Core/Interfaces/ISyncService.cs` - Interface with sync operations, events, and result types
- `src/Bartleby.Services/SyncService.cs` - Implementation with bidirectional sync logic
- `tests/Bartleby.Services.Tests/SyncServiceTests.cs` - 35 unit tests

### Key Design Decisions

**Conflict Resolution Strategy:**
- **Content (title, description, labels)**: GitHub wins (source of truth)
- **Status**: Local wins - Bartleby-managed statuses (Ready, InProgress, Blocked, Complete, Failed) are pushed back to GitHub
- **Pending status**: Not pushed back (it's the default state, let source control it)

**Sync Algorithm:**
1. Fetch all items from GitHub via `IWorkSource.SyncAsync()`
2. For each remote item:
   - If no local match: Create locally
   - If local exists: Merge remote content into local, preserve local status if different
3. If local status differs and is a Bartleby-managed status: Push status to GitHub
4. Remove local items that no longer exist in remote (for same source)

**Features:**
- Concurrent sync prevention (only one sync at a time)
- Events: `SyncStarted`, `SyncCompleted`, `ItemSynced`
- Duration tracking and last sync timestamp
- Proper error handling with failure results

### Test Coverage (35 tests)
- Constructor validation
- Initial state verification
- New items added to local store
- Existing items updated (preserving local-only fields)
- Items removed when deleted from source
- Status conflict resolution (all status types)
- Event emission for all sync actions
- Concurrent sync prevention
- Last sync time tracking
- Error handling and cancellation
- Duration tracking

---

**Story**: Testable unit of code | **Parent Epic**: #3 GitHub Integration

---
*Cached from GitHub: 2025-12-28*

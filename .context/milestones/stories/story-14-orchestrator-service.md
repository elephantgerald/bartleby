# Story #14: Implement OrchestratorService background service

**GitHub URL:** https://github.com/elephantgerald/bartleby/issues/14
**State:** Closed
**Labels:** `story`, `phase-5`
**Milestone:** [Phase 5: Orchestrator Service](../milestone-4-orchestrator-service.md)

## Overview

The core background service that runs Bartleby's work loop. This is the heart of the scrivener.

## Tasks

- [x] Create `OrchestratorService.cs` in `Bartleby.Services/`
- [x] Implement as .NET background service (IHostedService or BackgroundService)
- [x] Timer-based execution with configurable interval
- [x] State machine: Ready → InProgress → Blocked/Complete/Failed
- [x] Coordinate: DependencyResolver → WorkExecutor → Status updates
- [x] Respect quiet hours and token budgets
- [x] Emit events for UI updates

## Testing Requirements

- [x] Unit tests for service lifecycle (start, stop, restart)
- [x] Unit tests for timer behavior
- [x] Unit tests for state machine transitions
- [x] Unit tests for coordination flow
- [x] Tests for budget enforcement
- [x] Tests for quiet hours

## Acceptance Criteria

- [x] Service starts and runs in background
- [x] Work items progress through states correctly
- [x] Budgets and schedules are respected
- [x] Service can be gracefully stopped
- [x] All tests pass (46 new tests)

## Implementation Notes

### Files Created
- `src/Bartleby.Core/Interfaces/IOrchestratorService.cs` - Interface with state machine, events, stats
- `src/Bartleby.Services/OrchestratorService.cs` - Full implementation with timer, quiet hours, budget tracking
- `tests/Bartleby.Services.Tests/OrchestratorServiceTests.cs` - 46 unit tests

### Files Modified
- `src/Bartleby.Core/Models/AppSettings.cs` - Added quiet hours and token budget settings
- `src/Bartleby.App/MauiProgram.cs` - Registered IOrchestratorService and IDependencyResolver

### Key Features
- **State Machine**: Stopped → Starting → Idle → Working → QuietHours/BudgetExhausted → Stopping
- **Quiet Hours**: Configurable overnight or same-day time ranges
- **Token Budget**: Daily budget with automatic midnight reset
- **Events**: StateChanged and WorkItemStatusChanged for UI updates
- **ITimeProvider**: Abstracted time for testability

---
**Story**: Testable unit of code | **Parent Epic**: #5 Orchestrator Service

---
*Cached from GitHub: 2025-12-29*

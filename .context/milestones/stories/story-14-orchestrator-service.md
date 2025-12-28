# Story #14: Implement OrchestratorService background service

**GitHub URL:** https://github.com/elephantgerald/bartleby/issues/14
**State:** Open
**Labels:** `story`, `phase-5`
**Milestone:** [Phase 5: Orchestrator Service](../milestone-4-orchestrator-service.md)

## Overview

The core background service that runs Bartleby's work loop. This is the heart of the scrivener.

## Tasks

- [ ] Create `OrchestratorService.cs` in `Bartleby.Services/`
- [ ] Implement as .NET background service (IHostedService or BackgroundService)
- [ ] Timer-based execution with configurable interval
- [ ] State machine: Ready → InProgress → Blocked/Complete/Failed
- [ ] Coordinate: DependencyResolver → WorkExecutor → Status updates
- [ ] Respect quiet hours and token budgets
- [ ] Emit events for UI updates

## Testing Requirements

- [ ] Unit tests for service lifecycle (start, stop, restart)
- [ ] Unit tests for timer behavior
- [ ] Unit tests for state machine transitions
- [ ] Unit tests for coordination flow
- [ ] Tests for budget enforcement
- [ ] Tests for quiet hours

## Acceptance Criteria

- [ ] Service starts and runs in background
- [ ] Work items progress through states correctly
- [ ] Budgets and schedules are respected
- [ ] Service can be gracefully stopped
- [ ] All tests pass

---
**Story**: Testable unit of code | **Parent Epic**: #5 Orchestrator Service

---
*Cached from GitHub: 2025-12-28*

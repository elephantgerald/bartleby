# Story #9: Implement DependencyResolver

**GitHub URL:** https://github.com/elephantgerald/bartleby/issues/9
**State:** Open
**Labels:** `story`, `phase-2`
**Milestone:** [Phase 2: PlantUML & Dependency Resolution](../milestone-1-plantuml-dependency.md)

## Overview

Determine which work items are ready to be worked on based on their dependency status.

## Tasks

- [ ] Create `DependencyResolver.cs` in `Bartleby.Services/`
- [ ] Load dependency graph from `IGraphStore`
- [ ] Match work items to graph nodes by ID/external ID
- [ ] Identify work items with all dependencies complete
- [ ] Detect and report circular dependencies
- [ ] Return prioritized list of "ready" work items

## Testing Requirements

- [ ] Unit tests for work items with no dependencies (always ready)
- [ ] Unit tests for work items with met dependencies
- [ ] Unit tests for work items with unmet dependencies
- [ ] Unit tests for circular dependency detection
- [ ] Unit tests for mixed scenarios

## Acceptance Criteria

- [ ] Resolver correctly identifies which items are ready
- [ ] Circular dependencies don't cause infinite loops
- [ ] All tests pass

---
**Story**: Testable unit of code | **Parent Epic**: #2 PlantUML & Dependency Resolution

---
*Cached from GitHub: 2025-12-28*

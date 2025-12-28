# Story #7: Set up test infrastructure

**GitHub URL:** https://github.com/elephantgerald/bartleby/issues/7
**State:** Open
**Labels:** `story`, `infrastructure`
**Milestone:** None (Infrastructure)

## Overview

Create the test project structure with xUnit and configure testing conventions.

## Tasks

- [ ] Create `tests/Bartleby.Core.Tests` project
- [ ] Create `tests/Bartleby.Infrastructure.Tests` project
- [ ] Create `tests/Bartleby.Services.Tests` project
- [ ] Add xUnit, FluentAssertions, Moq NuGet packages
- [ ] Configure project references
- [ ] Add sample test to verify setup works

## Testing Requirements

- All test projects build successfully
- `dotnet test` runs and passes
- Test coverage reporting configured (optional)

## Acceptance Criteria

- [ ] Test projects exist and are referenced in solution
- [ ] Running `dotnet test` from solution root works
- [ ] At least one passing test exists

---
**Story**: Testable unit of code | **Parent Epic**: All epics depend on this

---
*Cached from GitHub: 2025-12-28*

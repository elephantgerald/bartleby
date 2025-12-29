# Story #15: Implement blocked work management

**GitHub URL:** https://github.com/elephantgerald/bartleby/issues/15
**State:** Closed
**Labels:** `story`, `phase-5`
**Milestone:** [Phase 5: Orchestrator Service](../milestone-4-orchestrator-service.md)

## Overview

Handle situations where AI needs clarification, and manage the Q&A flow.

## Tasks

- [x] Detect when AI response indicates "blocked" state
- [x] Generate clarifying questions from AI context
- [x] Store blocked questions in `BlockedQuestionRepository`
- [x] Create UI notification for blocked items
- [x] Accept answers via BlockedPage UI
- [x] Resume work item processing with answer context
- [x] Track Q&A history in transformation log

## Testing Requirements

- [x] Unit tests for blocked state detection
- [x] Unit tests for question generation
- [x] Unit tests for answer acceptance
- [x] Unit tests for work resumption
- [x] Integration tests for full Q&A flow

## Acceptance Criteria

- [x] Blocked items appear in Blocked page
- [x] Questions are clear and actionable
- [x] Answers are incorporated into subsequent AI calls
- [x] Work resumes after answer is provided
- [x] All tests pass

## Implementation Notes

Most infrastructure was already implemented in previous stories. This story completed the feature by adding:

### Files Modified
- `src/Bartleby.App/ViewModels/DashboardViewModel.cs` - Added `HasBlockedItems` computed property
- `src/Bartleby.App/Views/DashboardPage.xaml` - Added visual notification with DataTrigger
- `src/Bartleby.App/Views/DashboardPage.xaml.cs` - Added pulsing animation for blocked items

### Files Created
- `tests/Bartleby.Services.Tests/BlockedWorkFlowTests.cs` - 5 integration tests for full Q&A flow

### Test Coverage
- 302 total tests passing
- New tests verify:
  - AI blocked responses create questions in repository
  - Answered questions are included in next AI prompt
  - `GetNextTransformationAsync` returns `AskClarification` when unanswered questions exist
  - Full Q&A flow from blocked → answered → resumed

---
**Story**: Testable unit of code | **Parent Epic**: #5 Orchestrator Service

---
*Updated: 2025-12-29*

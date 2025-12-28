# Story #15: Implement blocked work management

**GitHub URL:** https://github.com/elephantgerald/bartleby/issues/15
**State:** Open
**Labels:** `story`, `phase-5`
**Milestone:** [Phase 5: Orchestrator Service](../milestone-4-orchestrator-service.md)

## Overview

Handle situations where AI needs clarification, and manage the Q&A flow.

## Tasks

- [ ] Detect when AI response indicates "blocked" state
- [ ] Generate clarifying questions from AI context
- [ ] Store blocked questions in `BlockedQuestionRepository`
- [ ] Create UI notification for blocked items
- [ ] Accept answers via BlockedPage UI
- [ ] Resume work item processing with answer context
- [ ] Track Q&A history in transformation log

## Testing Requirements

- [ ] Unit tests for blocked state detection
- [ ] Unit tests for question generation
- [ ] Unit tests for answer acceptance
- [ ] Unit tests for work resumption
- [ ] Integration tests for full Q&A flow

## Acceptance Criteria

- [ ] Blocked items appear in Blocked page
- [ ] Questions are clear and actionable
- [ ] Answers are incorporated into subsequent AI calls
- [ ] Work resumes after answer is provided
- [ ] All tests pass

---
**Story**: Testable unit of code | **Parent Epic**: #5 Orchestrator Service

---
*Cached from GitHub: 2025-12-28*

# Story #13: Implement WorkExecutor with prompt templates

**GitHub URL:** https://github.com/elephantgerald/bartleby/issues/13
**State:** Open
**Labels:** `story`, `phase-4`
**Milestone:** [Phase 4: Azure OpenAI Integration](../milestone-3-azure-openai.md)

## Overview

Orchestrate AI work execution with structured prompts and response handling.

## Tasks

- [ ] Create `WorkExecutor.cs` in `Bartleby.Services/`
- [ ] Design prompt templates for different transformation types (interpret, plan, execute, refine)
- [ ] Build work context from WorkItem + project + history
- [ ] Send prompts to IAIProvider
- [ ] Parse AI responses into actionable results
- [ ] Handle response types: code changes, questions, completion, failure
- [ ] Store transformation history with provenance

## Testing Requirements

- [ ] Unit tests for prompt template generation
- [ ] Unit tests for context building
- [ ] Unit tests for response parsing (each response type)
- [ ] Unit tests with mock AI provider
- [ ] Tests for provenance tracking

## Acceptance Criteria

- [ ] Prompts include relevant context for the work item
- [ ] AI responses are correctly parsed and categorized
- [ ] Transformation history is recorded
- [ ] All tests pass

---
**Story**: Testable unit of code | **Parent Epic**: #4 Azure OpenAI Integration

---
*Cached from GitHub: 2025-12-28*

# Story #13: Implement WorkExecutor with prompt templates

**GitHub URL:** https://github.com/elephantgerald/bartleby/issues/13
**State:** Closed
**Labels:** `story`, `phase-4`
**Milestone:** [Phase 4: Azure OpenAI Integration](../milestone-3-azure-openai.md)

## Overview

Orchestrate AI work execution with structured prompts and response handling.

## Tasks

- [x] Create `WorkExecutor.cs` in `Bartleby.Services/`
- [x] Design prompt templates for different transformation types (interpret, plan, execute, refine)
- [x] Build work context from WorkItem + project + history
- [x] Send prompts to IAIProvider
- [x] Parse AI responses into actionable results
- [x] Handle response types: code changes, questions, completion, failure
- [x] Store transformation history with provenance

## Testing Requirements

- [x] Unit tests for prompt template generation
- [x] Unit tests for context building
- [x] Unit tests for response parsing (each response type)
- [x] Unit tests with mock AI provider
- [x] Tests for provenance tracking

## Acceptance Criteria

- [x] Prompts include relevant context for the work item
- [x] AI responses are correctly parsed and categorized
- [x] Transformation history is recorded
- [x] All tests pass

## Implementation Notes

### Files Created
- `src/Bartleby.Core/Models/TransformationType.cs` - Enum for transformation phases (Interpret, Plan, Execute, Refine, AskClarification, Finalize)
- `src/Bartleby.Core/Models/WorkExecutionContext.cs` - Context record aggregating WorkItem, history, and answered questions
- `src/Bartleby.Core/Models/WorkExecutionResponse.cs` - Response record with provenance tracking
- `src/Bartleby.Core/Interfaces/IWorkExecutor.cs` - Interface for the WorkExecutor service
- `src/Bartleby.Services/Prompts/PromptTemplateProvider.cs` - Static class providing transformation-specific prompts
- `src/Bartleby.Services/WorkExecutor.cs` - Main service implementation
- `tests/Bartleby.Services.Tests/WorkExecutorTests.cs` - 38 unit tests for WorkExecutor
- `tests/Bartleby.Services.Tests/Prompts/PromptTemplateProviderTests.cs` - Tests for prompt templates

### Files Modified
- `src/Bartleby.Core/Interfaces/IAIProvider.cs` - Added `ExecutePromptAsync` method
- `src/Bartleby.Core/Models/WorkSession.cs` - Added `TransformationType` property for provenance
- `src/Bartleby.Infrastructure/AIProviders/AzureOpenAIProvider.cs` - Implemented `ExecutePromptAsync`
- `src/Bartleby.Infrastructure/AIProviders/StubAIProvider.cs` - Implemented `ExecutePromptAsync`
- `src/Bartleby.App/MauiProgram.cs` - Registered IWorkExecutor/WorkExecutor in DI

### Key Features
- **Transformation Types**: Interpret, Plan, Execute, Refine, AskClarification, Finalize
- **Context Building**: Aggregates work item, previous sessions, answered questions
- **Provenance Tracking**: WorkSession records transformation type and outcome
- **Blocked Questions**: Creates BlockedQuestion entities when AI needs input
- **Attempt Tracking**: Updates work item LastWorkedAt and AttemptCount

### Test Coverage
- 73 total tests in Bartleby.Services.Tests (including existing DependencyResolver and SyncService tests)
- Constructor validation tests
- ExecuteAsync success, failure, and blocked scenarios
- BuildContextAsync with sessions and questions
- GetNextTransformationAsync state machine logic
- PromptTemplateProvider for all transformation types

---
**Story**: Testable unit of code | **Parent Epic**: #4 Azure OpenAI Integration

---
*Cached from GitHub: 2025-12-28*

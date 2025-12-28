# Story #12: Implement AzureOpenAIProvider

**GitHub URL:** https://github.com/elephantgerald/bartleby/issues/12
**State:** Open
**Labels:** `story`, `phase-4`
**Milestone:** [Phase 4: Azure OpenAI Integration](../milestone-3-azure-openai.md)

## Overview

Replace `StubAIProvider` with real Azure OpenAI integration.

## Tasks

- [ ] Add Azure.AI.OpenAI NuGet package
- [ ] Create `AzureOpenAIProvider.cs` in `Bartleby.Infrastructure/AIProviders/`
- [ ] Implement authentication with Azure OpenAI endpoint
- [ ] Implement `ExecuteWorkAsync` to send prompts and receive responses
- [ ] Implement `GenerateQuestionsAsync` for clarification requests
- [ ] Track token usage for budget management
- [ ] Handle rate limiting and retries

## Testing Requirements

- [ ] Unit tests with mocked Azure OpenAI client
- [ ] Integration tests with real API (optional, requires credentials)
- [ ] Tests for authentication failure handling
- [ ] Tests for rate limiting behavior
- [ ] Tests for token counting accuracy

## Acceptance Criteria

- [ ] Can connect to Azure OpenAI with valid credentials
- [ ] Work items can be processed by AI
- [ ] Token usage is accurately tracked
- [ ] Errors are handled gracefully
- [ ] All tests pass

---
**Story**: Testable unit of code | **Parent Epic**: #4 Azure OpenAI Integration

---
*Cached from GitHub: 2025-12-28*

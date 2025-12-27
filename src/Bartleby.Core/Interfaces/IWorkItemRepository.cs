using Bartleby.Core.Models;

namespace Bartleby.Core.Interfaces;

/// <summary>
/// Repository for work item persistence.
/// </summary>
public interface IWorkItemRepository
{
    Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkItem?> GetByExternalIdAsync(string source, string externalId, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkItem>> GetByStatusAsync(WorkItemStatus status, CancellationToken cancellationToken = default);
    Task<WorkItem> CreateAsync(WorkItem workItem, CancellationToken cancellationToken = default);
    Task<WorkItem> UpdateAsync(WorkItem workItem, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for blocked questions.
/// </summary>
public interface IBlockedQuestionRepository
{
    Task<BlockedQuestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<BlockedQuestion>> GetByWorkItemIdAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<IEnumerable<BlockedQuestion>> GetUnansweredAsync(CancellationToken cancellationToken = default);
    Task<BlockedQuestion> CreateAsync(BlockedQuestion question, CancellationToken cancellationToken = default);
    Task<BlockedQuestion> UpdateAsync(BlockedQuestion question, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for work sessions.
/// </summary>
public interface IWorkSessionRepository
{
    Task<WorkSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkSession>> GetByWorkItemIdAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<WorkSession> CreateAsync(WorkSession session, CancellationToken cancellationToken = default);
    Task<WorkSession> UpdateAsync(WorkSession session, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for app settings.
/// </summary>
public interface ISettingsRepository
{
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

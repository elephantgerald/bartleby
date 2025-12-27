using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;

namespace Bartleby.Infrastructure.Persistence;

public class WorkItemRepository : IWorkItemRepository
{
    private readonly LiteDbContext _context;

    public WorkItemRepository(LiteDbContext context)
    {
        _context = context;
    }

    public Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = _context.WorkItems.FindById(id);
        return Task.FromResult(item);
    }

    public Task<WorkItem?> GetByExternalIdAsync(string source, string externalId, CancellationToken cancellationToken = default)
    {
        var item = _context.WorkItems.FindOne(x => x.Source == source && x.ExternalId == externalId);
        return Task.FromResult(item);
    }

    public Task<IEnumerable<WorkItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = _context.WorkItems.FindAll();
        return Task.FromResult(items);
    }

    public Task<IEnumerable<WorkItem>> GetByStatusAsync(WorkItemStatus status, CancellationToken cancellationToken = default)
    {
        var items = _context.WorkItems.Find(x => x.Status == status);
        return Task.FromResult(items);
    }

    public Task<WorkItem> CreateAsync(WorkItem workItem, CancellationToken cancellationToken = default)
    {
        workItem.CreatedAt = DateTime.UtcNow;
        workItem.UpdatedAt = DateTime.UtcNow;
        _context.WorkItems.Insert(workItem);
        return Task.FromResult(workItem);
    }

    public Task<WorkItem> UpdateAsync(WorkItem workItem, CancellationToken cancellationToken = default)
    {
        workItem.UpdatedAt = DateTime.UtcNow;
        _context.WorkItems.Update(workItem);
        return Task.FromResult(workItem);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _context.WorkItems.Delete(id);
        return Task.CompletedTask;
    }
}

public class BlockedQuestionRepository : IBlockedQuestionRepository
{
    private readonly LiteDbContext _context;

    public BlockedQuestionRepository(LiteDbContext context)
    {
        _context = context;
    }

    public Task<BlockedQuestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = _context.BlockedQuestions.FindById(id);
        return Task.FromResult(item);
    }

    public Task<IEnumerable<BlockedQuestion>> GetByWorkItemIdAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        var items = _context.BlockedQuestions.Find(x => x.WorkItemId == workItemId);
        return Task.FromResult(items);
    }

    public Task<IEnumerable<BlockedQuestion>> GetUnansweredAsync(CancellationToken cancellationToken = default)
    {
        var items = _context.BlockedQuestions.Find(x => x.Answer == null);
        return Task.FromResult(items);
    }

    public Task<BlockedQuestion> CreateAsync(BlockedQuestion question, CancellationToken cancellationToken = default)
    {
        question.CreatedAt = DateTime.UtcNow;
        _context.BlockedQuestions.Insert(question);
        return Task.FromResult(question);
    }

    public Task<BlockedQuestion> UpdateAsync(BlockedQuestion question, CancellationToken cancellationToken = default)
    {
        _context.BlockedQuestions.Update(question);
        return Task.FromResult(question);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _context.BlockedQuestions.Delete(id);
        return Task.CompletedTask;
    }
}

public class WorkSessionRepository : IWorkSessionRepository
{
    private readonly LiteDbContext _context;

    public WorkSessionRepository(LiteDbContext context)
    {
        _context = context;
    }

    public Task<WorkSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = _context.WorkSessions.FindById(id);
        return Task.FromResult(item);
    }

    public Task<IEnumerable<WorkSession>> GetByWorkItemIdAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        var items = _context.WorkSessions.Find(x => x.WorkItemId == workItemId);
        return Task.FromResult(items);
    }

    public Task<WorkSession> CreateAsync(WorkSession session, CancellationToken cancellationToken = default)
    {
        session.StartedAt = DateTime.UtcNow;
        _context.WorkSessions.Insert(session);
        return Task.FromResult(session);
    }

    public Task<WorkSession> UpdateAsync(WorkSession session, CancellationToken cancellationToken = default)
    {
        _context.WorkSessions.Update(session);
        return Task.FromResult(session);
    }
}

public class SettingsRepository : ISettingsRepository
{
    private readonly LiteDbContext _context;
    private static readonly Guid DefaultSettingsId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public SettingsRepository(LiteDbContext context)
    {
        _context = context;
    }

    public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = _context.Settings.FindById(DefaultSettingsId);
        if (settings is null)
        {
            settings = new AppSettings { Id = DefaultSettingsId };
            _context.Settings.Insert(settings);
        }
        return Task.FromResult(settings);
    }

    public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Id = DefaultSettingsId;
        _context.Settings.Upsert(settings);
        return Task.CompletedTask;
    }
}

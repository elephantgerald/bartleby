using LiteDB;
using Bartleby.Core.Models;

namespace Bartleby.Infrastructure.Persistence;

/// <summary>
/// LiteDB database context for Bartleby.
/// </summary>
public class LiteDbContext : IDisposable
{
    private readonly LiteDatabase _database;
    private bool _disposed;

    public LiteDbContext(string connectionString = "Bartleby.db")
    {
        _database = new LiteDatabase(connectionString);

        // Configure BSON mappings
        ConfigureMappings();
    }

    public ILiteCollection<WorkItem> WorkItems => _database.GetCollection<WorkItem>("work_items");
    public ILiteCollection<BlockedQuestion> BlockedQuestions => _database.GetCollection<BlockedQuestion>("blocked_questions");
    public ILiteCollection<WorkSession> WorkSessions => _database.GetCollection<WorkSession>("work_sessions");
    public ILiteCollection<AppSettings> Settings => _database.GetCollection<AppSettings>("settings");

    private void ConfigureMappings()
    {
        var mapper = BsonMapper.Global;

        mapper.Entity<WorkItem>()
            .Id(x => x.Id);

        mapper.Entity<BlockedQuestion>()
            .Id(x => x.Id);

        mapper.Entity<WorkSession>()
            .Id(x => x.Id);

        mapper.Entity<AppSettings>()
            .Id(x => x.Id);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _database.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

namespace Bartleby.Core.Models;

public class BlockedQuestion
{
    /// <summary>
    /// Unique identifier for this question.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The work item this question is associated with.
    /// </summary>
    public Guid WorkItemId { get; set; }

    /// <summary>
    /// The question text that needs to be answered.
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Context about why this question was asked.
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// The answer provided by the user (null if unanswered).
    /// </summary>
    public string? Answer { get; set; }

    /// <summary>
    /// When the question was generated.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the question was answered (null if unanswered).
    /// </summary>
    public DateTime? AnsweredAt { get; set; }

    /// <summary>
    /// Whether this question has been answered.
    /// </summary>
    public bool IsAnswered => Answer is not null;
}

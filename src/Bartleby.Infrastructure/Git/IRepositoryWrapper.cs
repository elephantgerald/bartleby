using LibGit2Sharp;

namespace Bartleby.Infrastructure.Git;

/// <summary>
/// Wrapper interface for LibGit2Sharp Repository to enable testing.
/// </summary>
public interface IRepositoryWrapper : IDisposable
{
    /// <summary>
    /// Gets the repository head.
    /// </summary>
    Branch Head { get; }

    /// <summary>
    /// Gets the repository branches.
    /// </summary>
    BranchCollection Branches { get; }

    /// <summary>
    /// Gets the repository index.
    /// </summary>
    LibGit2Sharp.Index Index { get; }

    /// <summary>
    /// Gets the repository status.
    /// </summary>
    RepositoryStatus RetrieveStatus(StatusOptions? options = null);

    /// <summary>
    /// Gets the repository network (remotes).
    /// </summary>
    Network Network { get; }

    /// <summary>
    /// Creates a commit.
    /// </summary>
    Commit Commit(string message, Signature author, Signature committer, CommitOptions? options = null);

    /// <summary>
    /// Checks out the specified branch.
    /// </summary>
    Branch Checkout(Branch branch, CheckoutOptions? options = null);

    /// <summary>
    /// Checks out the specified committish.
    /// </summary>
    Branch Checkout(string committishOrBranchSpec, CheckoutOptions? options = null);

    /// <summary>
    /// Creates a new branch.
    /// </summary>
    Branch CreateBranch(string branchName);

    /// <summary>
    /// Creates a new branch from a specific commit.
    /// </summary>
    Branch CreateBranch(string branchName, Commit commit);

    /// <summary>
    /// Stages a file.
    /// </summary>
    void Stage(string path);

    /// <summary>
    /// Stages multiple files.
    /// </summary>
    void Stage(IEnumerable<string> paths);

    /// <summary>
    /// Gets the repository info.
    /// </summary>
    RepositoryInformation Info { get; }

    /// <summary>
    /// Gets the repository configuration.
    /// </summary>
    Configuration Config { get; }
}

/// <summary>
/// Real implementation wrapping LibGit2Sharp Repository.
/// </summary>
internal sealed class RepositoryWrapper : IRepositoryWrapper
{
    private readonly Repository _repository;

    public RepositoryWrapper(string path)
    {
        _repository = new Repository(path);
    }

    public Branch Head => _repository.Head;
    public BranchCollection Branches => _repository.Branches;
    public LibGit2Sharp.Index Index => _repository.Index;
    public Network Network => _repository.Network;
    public RepositoryInformation Info => _repository.Info;
    public Configuration Config => _repository.Config;

    public RepositoryStatus RetrieveStatus(StatusOptions? options = null)
        => _repository.RetrieveStatus(options ?? new StatusOptions());

    public Commit Commit(string message, Signature author, Signature committer, CommitOptions? options = null)
        => _repository.Commit(message, author, committer, options);

    public Branch Checkout(Branch branch, CheckoutOptions? options = null)
        => Commands.Checkout(_repository, branch, options ?? new CheckoutOptions());

    public Branch Checkout(string committishOrBranchSpec, CheckoutOptions? options = null)
        => Commands.Checkout(_repository, committishOrBranchSpec, options ?? new CheckoutOptions());

    public Branch CreateBranch(string branchName)
        => _repository.CreateBranch(branchName);

    public Branch CreateBranch(string branchName, Commit commit)
        => _repository.CreateBranch(branchName, commit);

    public void Stage(string path)
        => Commands.Stage(_repository, path);

    public void Stage(IEnumerable<string> paths)
        => Commands.Stage(_repository, paths);

    public void Dispose()
        => _repository.Dispose();
}

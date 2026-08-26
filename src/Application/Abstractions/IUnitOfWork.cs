using System;
using System.Threading;
using System.Threading.Tasks;

namespace BaseRepository.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a transaction spanning several SaveChangesAsync calls, or spanning a save and
    /// work after it that can still fail. Dispose without committing and the whole thing rolls
    /// back, so a handler cannot leave half of an operation persisted.
    /// </summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// An open transaction. Commit explicitly; disposing an uncommitted transaction rolls it back,
/// which is what makes <c>await using</c> the safe default.
/// </summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}

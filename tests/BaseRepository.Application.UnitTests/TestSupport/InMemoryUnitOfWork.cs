using System;
using System.Threading;
using System.Threading.Tasks;
using BaseRepository.Application.Abstractions;

namespace BaseRepository.Application.UnitTests.TestSupport;

public class InMemoryUnitOfWork : IUnitOfWork
{
    /// <summary>The most recently started transaction, so a test can assert how it ended.</summary>
    public FakeTransaction? LastTransaction { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        LastTransaction = new FakeTransaction();
        return Task.FromResult<IUnitOfWorkTransaction>(LastTransaction);
    }
}

/// <summary>
/// Records how a transaction ended. It does not undo writes to the in-memory repository -
/// that a rollback really discards the insert is proven against SQLite in
/// BaseRepository.Infrastructure.IntegrationTests.
/// </summary>
public class FakeTransaction : IUnitOfWorkTransaction
{
    public bool Committed { get; private set; }
    public bool Disposed { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        Committed = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using BaseRepository.Domain.Entities;
using BaseRepository.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BaseRepository.Infrastructure.IntegrationTests.Persistence;

/// <summary>
/// Proves IUnitOfWork.BeginTransactionAsync really is transactional against a database, not
/// just in the Application-layer fake: an uncommitted transaction must discard writes that
/// SaveChangesAsync already issued. RegisterCommandHandler depends on exactly this - it
/// inserts the user, then issues a token that can fail, and must not leave the user behind.
/// </summary>
public class UnitOfWorkTransactionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly UnitOfWork _unitOfWork;

    public UnitOfWorkTransactionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
        _unitOfWork = new UnitOfWork(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task DisposingWithoutCommitting_DiscardsASavedInsert()
    {
        await using (await _unitOfWork.BeginTransactionAsync(CancellationToken.None))
        {
            _context.Users.Add(new User { Email = "rolled-back@example.com", PasswordHash = "x" });
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        _context.ChangeTracker.Clear();
        Assert.False(await _context.Users.AnyAsync(u => u.Email == "rolled-back@example.com"));
    }

    [Fact]
    public async Task Committing_KeepsTheInsert()
    {
        await using (var transaction = await _unitOfWork.BeginTransactionAsync(CancellationToken.None))
        {
            _context.Users.Add(new User { Email = "committed@example.com", PasswordHash = "x" });
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
        }

        _context.ChangeTracker.Clear();
        Assert.True(await _context.Users.AnyAsync(u => u.Email == "committed@example.com"));
    }

    [Fact]
    public async Task RollingBackExplicitly_DiscardsASavedInsert()
    {
        await using (var transaction = await _unitOfWork.BeginTransactionAsync(CancellationToken.None))
        {
            _context.Users.Add(new User { Email = "explicit@example.com", PasswordHash = "x" });
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            await transaction.RollbackAsync(CancellationToken.None);
        }

        _context.ChangeTracker.Clear();
        Assert.False(await _context.Users.AnyAsync(u => u.Email == "explicit@example.com"));
    }
}

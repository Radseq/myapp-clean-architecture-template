using MyApp.Domain.Common;

namespace MyApp.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken ct);

    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken ct);

    // specjalnie dla MessageResult -> commit tylko gdy IsSuccess, rollback gdy Fail
    Task<MessageResult> ExecuteInTransactionResultAsync(
        Func<CancellationToken, Task<MessageResult>> action,
        CancellationToken ct);

    Task<MessageResult<T>> ExecuteInTransactionResultAsync<T>(
        Func<CancellationToken, Task<MessageResult<T>>> action,
        CancellationToken ct);


    /// <summary>
    /// Persists changes. Returns affected rows count on success.
    /// Maps EF exceptions to MessageResult errors (no exceptions as flow).
    /// </summary>
    Task<MessageResult<int>> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Begins a database transaction (optional). Use when you need atomicity across multiple writes.
    /// DONT USE WHEN YOU DO DB RETRY LIKE sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), errorNumbersToAdd: null)
    /// </summary>
    Task<MessageResult<IUnitOfWorkTransaction>> BeginTransactionAsync(CancellationToken ct = default);

	/// <summary>
	/// Registers callbacks executed only after successful SaveChanges.
	/// Useful e.g. for copying generated identity IDs back to domain objects.
	/// </summary>
	void EnqueuePostSave(Action action);

	// ważne: jak komenda zwróci failure i nie zapisujemy,
	// to czyścimy kolejkę, żeby nie wykonała się "przy okazji" w tej samej scoped instancji
	void ClearPostSaveQueue();
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task<MessageResult> CommitAsync(CancellationToken ct = default);
    Task<MessageResult> CommitWithSaveAsync(CancellationToken ct = default);
    Task<MessageResult> RollbackAsync(CancellationToken ct = default);
}
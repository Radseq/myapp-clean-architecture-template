using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions.Persistence;
using MyApp.Application.Common;
using MyApp.Domain.Common;
using MyApp.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst;
using System.Text.RegularExpressions;

namespace MyApp.Infrastructure.Persistence;

public sealed class EfUnitOfWork(AppDbContext db, ILogger<EfUnitOfWork> logger)
    : IUnitOfWork, IAsyncDisposable
{
    private readonly List<Action> _postSaveActions = [];

    private IDbContextTransaction? _tx;
    private int _depth;
    private bool _rollbackRequested;
    private bool _disposed;

    public void EnqueuePostSave(Action action)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(action);
        _postSaveActions.Add(action);
    }

    public void ClearPostSaveQueue() => _postSaveActions.Clear();

    public async Task<MessageResult<int>> SaveChangesAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        int affected;
        try
        {
            affected = await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // cancellation NIE jest błędem – nie mapujemy
            MarkRollbackRequestedAndClearActions();
            logger.LogDebug("SaveChanges canceled. Depth={Depth} InScope={InScope}", _depth, IsInScope());
            throw;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MarkRollbackRequestedAndClearActions();
            logger.LogWarning(ex,
                "EF concurrency conflict during SaveChanges. Depth={Depth} InScope={InScope}",
                _depth, IsInScope());

            return MessageResult<int>.Fail(Errors.Db.Conflict);
        }
        catch (DbUpdateException ex)
        {
            MarkRollbackRequestedAndClearActions();

            var mapped = MapDbUpdateException(ex);

            if (mapped.IsClientConflict)
            {
                logger.LogWarning(ex,
                    "EF constraint conflict. Provider={Provider} Kind={Kind} Constraint={Constraint} Value={Value}",
                    db.Database.ProviderName,
                    mapped.Kind,
                    mapped.Constraint,
                    mapped.Value);
            }
            else
            {
                logger.LogError(ex,
                    "EF DbUpdateException. Provider={Provider} Kind={Kind}",
                    db.Database.ProviderName,
                    mapped.Kind);
            }

            return MessageResult<int>.Fail(mapped.Error);
        }
        catch (Exception ex)
        {
            MarkRollbackRequestedAndClearActions();
            logger.LogError(ex,
                "Unexpected exception during SaveChanges. Provider={Provider} Depth={Depth} InScope={InScope}",
                db.Database.ProviderName, _depth, IsInScope());

            return MessageResult<int>.Fail(Errors.Db.Unexpected).ForceBodyLogging();
        }

        ExecuteAndClearPostSaveActionsOrThrow();

        return MessageResult<int>.Ok(affected);
    }

    public async Task<MessageResult<IUnitOfWorkTransaction>> BeginTransactionAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        // IMPORTANT: jeśli masz EnableRetryOnFailure (np. SqlServerRetryingExecutionStrategy),
        // to nie wolno zaczynać transakcji "ręcznie" (BeginTransactionAsync). Wtedy EF rzuci:
        // "The configured execution strategy ... does not support user-initiated transactions".
        // Rozwiązanie: użyj IUnitOfWork.ExecuteInTransactionAsync(...), które opakowuje całość
        // w CreateExecutionStrategy().ExecuteAsync(...).
        var executionStrategy = db.Database.CreateExecutionStrategy();
        if (executionStrategy.RetriesOnFailure)
            return MessageResult<IUnitOfWorkTransaction>.Fail(Errors.Db.ExecutionStrategyRequiresExecuteInTransaction);

        if (_depth == 0)
        {
            _rollbackRequested = false;
        }

        if (db.Database.IsRelational() && _depth == 0)
        {
            try
            {
                _tx = await db.Database.BeginTransactionAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                logger.LogDebug("BeginTransaction canceled.");
                throw;
            }
            catch (Exception ex)
            {
                _tx = null;
                _rollbackRequested = false;

                logger.LogError(ex,
                    "Failed to begin EF transaction. Provider={Provider}",
                    db.Database.ProviderName);

                return MessageResult<IUnitOfWorkTransaction>.Fail(Errors.Db.Unexpected).ForceBodyLogging();
            }
        }

        _depth++;
        return MessageResult<IUnitOfWorkTransaction>.Ok(new EfUnitOfWorkTransaction(this));
    }

    // ---------------------------------------------------------------------
    // Retry-safe transaction execution (required when EnableRetryOnFailure)
    // ---------------------------------------------------------------------

    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        return ExecuteInTransactionCoreAsync(
            async c =>
            {
                await action(c);
                return true;
            },
            shouldCommit: _ => true,
            ct);
    }

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ExecuteInTransactionCoreAsync(
            action,
            shouldCommit: static r => r is not MessageResult mr || mr.IsSuccess,
            ct);
    }

    public Task<MessageResult> ExecuteInTransactionResultAsync(
        Func<CancellationToken, Task<MessageResult>> action,
        CancellationToken ct)
        => ExecuteInTransactionAsync(action, ct);

    public Task<MessageResult<T>> ExecuteInTransactionResultAsync<T>(
        Func<CancellationToken, Task<MessageResult<T>>> action,
        CancellationToken ct)
        => ExecuteInTransactionAsync(action, ct);

    private async Task<TResult> ExecuteInTransactionCoreAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        Func<TResult, bool> shouldCommit,
        CancellationToken ct)
    {
        ThrowIfDisposed();

        if (IsInScope())
            throw new InvalidOperationException(
                "ExecuteInTransactionAsync cannot be called inside an existing transaction scope. " +
                "Use a single outer transaction, or implement savepoints inside infrastructure if you need partial rollback.");

        var executionStrategy = db.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async strategyCt =>
        {
            // per-attempt reset (execution strategy can rerun the whole delegate)
            _rollbackRequested = false;

            try
            {
                _tx = await db.Database.BeginTransactionAsync(strategyCt);
                _depth = 1;

                var result = await action(strategyCt);

                var commit = !_rollbackRequested && shouldCommit(result);

                if (!commit)
                {
                    await TryRollbackNoThrowAsync(strategyCt);
                    MarkRollbackRequestedAndClearActions();
                    return result;
                }

                // NOTE: nie robimy tu automatycznego SaveChanges. To ma być jawne:
                // - pipeline behavior robi SaveChanges po next()
                // - w handlerze możesz zrobić SaveChanges ile chcesz (np. żeby dostać ID)

                await _tx.CommitAsync(strategyCt);
                return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                MarkRollbackRequestedAndClearActions();
                throw;
            }
            catch
            {
                await TryRollbackNoThrowAsync(ct);
                MarkRollbackRequestedAndClearActions();
                throw;
            }
            finally
            {
                await DisposeTxNoThrowAsync();
                _depth = 0;

                // Jeśli ktoś enqueue’ował akcje, a nie zrobił SaveChanges — nie pozwól wyciec do następnego requestu.
                _postSaveActions.Clear();

                _rollbackRequested = false;
            }
        }, ct);
    }

    private async Task<MessageResult> CommitScopeAsync(CancellationToken ct, bool saveBeforeCommit)
    {
        PopOrThrow();

        // inner commit
        if (_depth != 0)
        {
            if (!saveBeforeCommit)
                return MessageResult.Ok();

            if (_rollbackRequested)
            {
                MarkRollbackRequestedAndClearActions();
                return MessageResult.Ok();
            }

            if (!db.ChangeTracker.HasChanges())
                return MessageResult.Ok();

            var saveInner = await SaveChangesAsync(ct);
            return saveInner.IsSuccess ? MessageResult.Ok() : MessageResult.Fail(saveInner.Errors);
        }

        // outermost
        if (!db.Database.IsRelational())
        {
            try
            {
                if (_rollbackRequested)
                {
                    MarkRollbackRequestedAndClearActions();
                    return MessageResult.Ok();
                }

                var hasChanges = db.ChangeTracker.HasChanges();

                if (saveBeforeCommit)
                {
                    if (hasChanges)
                    {
                        var save = await SaveChangesAsync(ct);
                        if (!save.IsSuccess)
                            return MessageResult.Fail(save.Errors);
                    }
                }
                else
                {
                    if (hasChanges)
                    {
                        MarkRollbackRequestedAndClearActions();
                        return MessageResult.Fail(Errors.Db.PendingChanges);
                    }
                }

                // non-relational: "commit" == "ok"
                return MessageResult.Ok();
            }
            finally
            {
                _postSaveActions.Clear();
                _rollbackRequested = false;
            }
        }

        if (_tx is null)
        {
            MarkRollbackRequestedAndClearActions();
            logger.LogError("CommitScope called but transaction is null. Provider={Provider}", db.Database.ProviderName);
            return MessageResult.Fail(Errors.Db.Unexpected);
        }

        try
        {
            if (_rollbackRequested)
            {
                await _tx.RollbackAsync(ct);
                MarkRollbackRequestedAndClearActions();
                return MessageResult.Ok();
            }

            var hasChanges = db.ChangeTracker.HasChanges();

            if (saveBeforeCommit)
            {
                if (hasChanges)
                {
                    var save = await SaveChangesAsync(ct);
                    if (!save.IsSuccess)
                    {
                        await TryRollbackNoThrowAsync(ct);
                        return MessageResult.Fail(save.Errors);
                    }
                }
            }
            else
            {
                if (hasChanges)
                {
                    await TryRollbackNoThrowAsync(ct);
                    MarkRollbackRequestedAndClearActions();
                    return MessageResult.Fail(Errors.Db.PendingChanges);
                }
            }

            await _tx.CommitAsync(ct);
            return MessageResult.Ok();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            MarkRollbackRequestedAndClearActions();
            logger.LogDebug("Commit canceled. Depth={Depth}", _depth);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Commit failed. Provider={Provider}. Trying rollback best-effort.",
                db.Database.ProviderName);

            await TryRollbackNoThrowAsync(ct);
            MarkRollbackRequestedAndClearActions();
            return MessageResult.Fail(Errors.Db.Unexpected).ForceBodyLogging();
        }
        finally
        {
            await DisposeTxNoThrowAsync();

            _postSaveActions.Clear();

            _rollbackRequested = false;
        }
    }

    private async Task<MessageResult> RollbackScopeAsync(CancellationToken ct)
    {
        PopOrThrow();

        _rollbackRequested = true;

        if (_depth != 0)
            return MessageResult.Ok();

        if (!db.Database.IsRelational())
        {
            MarkRollbackRequestedAndClearActions();
            _rollbackRequested = false;
            return MessageResult.Ok();
        }

        if (_tx is null)
        {
            MarkRollbackRequestedAndClearActions();
            _rollbackRequested = false;

            logger.LogError("RollbackScope called but transaction is null. Provider={Provider}", db.Database.ProviderName);
            return MessageResult.Fail(Errors.Db.Unexpected);
        }

        try
        {
            await _tx.RollbackAsync(ct);
            return MessageResult.Ok();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            MarkRollbackRequestedAndClearActions();
            logger.LogDebug("Rollback canceled.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rollback failed. Provider={Provider}", db.Database.ProviderName);
            return MessageResult.Fail(Errors.Db.Unexpected).ForceBodyLogging();
        }
        finally
        {
            MarkRollbackRequestedAndClearActions();
            await DisposeTxNoThrowAsync();
            _rollbackRequested = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_tx is not null)
        {
            try
            {
                await _tx.RollbackAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "DisposeAsync: rollback best-effort failed.");
            }
            finally
            {
                await DisposeTxNoThrowAsync();
            }
        }

        _postSaveActions.Clear();
        _depth = 0;
        _rollbackRequested = false;

        GC.SuppressFinalize(this);
    }

    private sealed class EfUnitOfWorkTransaction(EfUnitOfWork uow) : IUnitOfWorkTransaction
    {
        private bool _completed;

        public async Task<MessageResult> CommitAsync(CancellationToken ct = default)
        {
            if (_completed) return MessageResult.Ok();
            var res = await uow.CommitScopeAsync(ct, saveBeforeCommit: false);
            _completed = true;
            return res;
        }

        public async Task<MessageResult> CommitWithSaveAsync(CancellationToken ct = default)
        {
            if (_completed) return MessageResult.Ok();
            var res = await uow.CommitScopeAsync(ct, saveBeforeCommit: true);
            _completed = true;
            return res;
        }

        public async Task<MessageResult> RollbackAsync(CancellationToken ct = default)
        {
            if (_completed) return MessageResult.Ok();
            var res = await uow.RollbackScopeAsync(ct);
            _completed = true;
            return res;
        }

        public async ValueTask DisposeAsync()
        {
            if (_completed) return;

            try
            {
                await uow.RollbackScopeAsync(CancellationToken.None);
            }
            catch
            {
                // best-effort
            }
            finally
            {
                _completed = true;
            }
        }
    }

    private bool IsInScope() => _depth > 0 || _tx is not null;

    private void ExecuteAndClearPostSaveActionsOrThrow()
    {
        if (_postSaveActions.Count == 0)
            return;

        try
        {
            foreach (var a in _postSaveActions)
                a();
        }
        catch (Exception ex)
        {
            // To jest BUG w kodzie (callback), nie “Db error”.
            logger.LogError(ex, "Post-save action failed. This indicates a bug in application code.");
            throw;
        }
        finally
        {
            _postSaveActions.Clear();
        }
    }

    private void MarkRollbackRequestedAndClearActions()
    {
        _rollbackRequested = true;
        _postSaveActions.Clear();
    }

    private async Task TryRollbackNoThrowAsync(CancellationToken ct)
    {
        if (_tx is null) return;

        try { await _tx.RollbackAsync(ct); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rollback best-effort failed.");
        }
    }

    private async Task DisposeTxNoThrowAsync()
    {
        var tx = _tx;
        _tx = null;

        if (tx is null) return;

        try { await tx.DisposeAsync(); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dispose transaction best-effort failed.");
        }
    }

    private void PopOrThrow()
    {
        if (_depth == 0)
            throw new InvalidOperationException("Commit/Rollback called without BeginTransaction.");
        _depth--;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EfUnitOfWork));
    }

    private sealed record DbMappedError(
        ErrorData Error,
        bool IsClientConflict,
        string Kind,
        string? Constraint,
        string? Value);

    private static DbMappedError MapDbUpdateException(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null)
            return new DbMappedError(Errors.Db.Unexpected, false, "no_inner", null, null);

        var t = inner.GetType();
        var fullName = t.FullName ?? t.Name;

        // --- MySQL: MySqlConnector / MySql.Data ---
        if (fullName is "MySqlConnector.MySqlException" or "MySql.Data.MySqlClient.MySqlException")
        {
            var number = TryGetInt(inner, "Number");
            var msg = inner.Message ?? "";

            // 1062 duplicate entry
            if (number == 1062)
            {
                var constraint = Sanitize(TryExtractMySqlKeyName(msg));
                var value = Sanitize(TryExtractMySqlDuplicateValue(msg));

                return new DbMappedError(
                    Error: Errors.Db.Duplicate,          // <-- PUBLIC, bez args
                    IsClientConflict: true,
                    Kind: "mysql_duplicate",
                    Constraint: constraint,
                    Value: value);
            }

            // 1451/1452 foreign key constraint fails
            if (number is 1451 or 1452)
            {
                var constraint = Sanitize(TryExtractMySqlConstraintName(msg));

                return new DbMappedError(
                    Error: Errors.Db.ForeignKey,         // <-- PUBLIC, bez args
                    IsClientConflict: true,
                    Kind: "mysql_fk",
                    Constraint: constraint,
                    Value: null);
            }

            return new DbMappedError(Errors.Db.Unexpected, false, $"mysql_{number}", null, null);
        }

        // --- SQL Server: Microsoft.Data.SqlClient / System.Data.SqlClient ---
        if (fullName is "Microsoft.Data.SqlClient.SqlException" or "System.Data.SqlClient.SqlException")
        {
            var number = TryGetInt(inner, "Number");
            var msg = inner.Message ?? "";

            // 2601/2627 duplicates
            if (number is 2601 or 2627)
            {
                var constraint = Sanitize(TryExtractSqlServerConstraintOrIndex(msg));
                var value = Sanitize(TryExtractSqlServerDuplicateValue(msg));

                return new DbMappedError(
                    Error: Errors.Db.Duplicate,
                    IsClientConflict: true,
                    Kind: "sqlserver_duplicate",
                    Constraint: constraint,
                    Value: value);
            }

            // 547 FK
            if (number == 547)
            {
                var constraint = Sanitize(TryExtractSqlServerFkConstraint(msg));

                return new DbMappedError(
                    Error: Errors.Db.ForeignKey,
                    IsClientConflict: true,
                    Kind: "sqlserver_fk",
                    Constraint: constraint,
                    Value: null);
            }

            return new DbMappedError(Errors.Db.Unexpected, false, $"sqlserver_{number}", null, null);
        }

        // --- PostgreSQL: Npgsql.PostgresException ---
        if (fullName == "Npgsql.PostgresException")
        {
            var sqlState = TryGetString(inner, "SqlState");
            var constraint = Sanitize(TryGetString(inner, "ConstraintName"));

            // 23505 unique_violation
            if (sqlState == "23505")
            {
                // UWAGA: Detail potrafi zawierać wartości - dlatego sanitize + limit
                var detail = Sanitize(TryGetString(inner, "Detail"));

                return new DbMappedError(
                    Error: Errors.Db.Duplicate,
                    IsClientConflict: true,
                    Kind: "pgsql_unique",
                    Constraint: constraint,
                    Value: detail);
            }

            // 23503 foreign_key_violation
            if (sqlState == "23503")
            {
                return new DbMappedError(
                    Error: Errors.Db.ForeignKey,
                    IsClientConflict: true,
                    Kind: "pgsql_fk",
                    Constraint: constraint,
                    Value: null);
            }

            return new DbMappedError(Errors.Db.Unexpected, false, $"pgsql_{sqlState}", constraint, null);
        }

        return new DbMappedError(Errors.Db.Unexpected, false, fullName, null, null);
    }

    // -------- helpers (reflection + regex) --------

    private static string? Sanitize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;

        // usuń CR/LF i przytnij, żeby nie pompować logów
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        const int max = 180;
        return s.Length <= max ? s : s[..max] + "...";
    }

    private static int? TryGetInt(object o, string propName)
    {
        var p = o.GetType().GetProperty(propName);
        if (p is null) return null;

        var v = p.GetValue(o);
        return v switch
        {
            int i => i,
            short s => s,
            long l => unchecked((int)l),
            _ => null
        };
    }

    private static string? TryGetString(object o, string propName)
    {
        var p = o.GetType().GetProperty(propName);
        return p?.GetValue(o) as string;
    }

    private static string? TryExtractMySqlKeyName(string msg)
    {
        // "... for key 'UQ_Orders_Number'" / "... for key 'PRIMARY'"
        var m = Regex.Match(msg, @"for key '([^']+)'", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? TryExtractMySqlDuplicateValue(string msg)
    {
        // "Duplicate entry 'X' for key ..."
        var m = Regex.Match(msg, @"Duplicate entry '([^']+)'", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? TryExtractMySqlConstraintName(string msg)
    {
        // "... CONSTRAINT `FK_Name` FOREIGN KEY ..."
        var m = Regex.Match(msg, @"CONSTRAINT `([^`]+)`", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? TryExtractSqlServerConstraintOrIndex(string msg)
    {
        // "Violation of UNIQUE KEY constraint 'UQ_X'..."
        var m1 = Regex.Match(msg, @"constraint '([^']+)'", RegexOptions.IgnoreCase);
        if (m1.Success) return m1.Groups[1].Value;

        // "with unique index 'IX_X'..."
        var m2 = Regex.Match(msg, @"unique index '([^']+)'", RegexOptions.IgnoreCase);
        if (m2.Success) return m2.Groups[1].Value;

        return null;
    }

    private static string? TryExtractSqlServerDuplicateValue(string msg)
    {
        // "The duplicate key value is (...)."
        var m = Regex.Match(msg, @"duplicate key value is \((.*?)\)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? TryExtractSqlServerFkConstraint(string msg)
    {
        // "... FOREIGN KEY constraint \"FK_X\" ..."
        var m1 = Regex.Match(msg, "FOREIGN KEY constraint \"([^\"]+)\"", RegexOptions.IgnoreCase);
        if (m1.Success) return m1.Groups[1].Value;

        // czasem w apostrofach
        var m2 = Regex.Match(msg, @"FOREIGN KEY constraint '([^']+)'", RegexOptions.IgnoreCase);
        if (m2.Success) return m2.Groups[1].Value;

        return null;
    }
}

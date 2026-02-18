using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions.Caching;
using System.Collections.Concurrent;

namespace MyApp.Infrastructure.Caching;

public sealed class MemoryAppCache : IAppCache, IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryAppCache> _logger;

    // Locki per klucz – single-flight
    private readonly ConcurrentDictionary<string, LockEntry> _locks = new();

    // Cleanup
    private readonly TimeSpan _lockIdleTtl = TimeSpan.FromMinutes(5);      // ile może “leżeć” nieużywany lock
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(1);  // jak często sprzątać
    private readonly Timer _cleanupTimer;

    public MemoryAppCache(IMemoryCache cache, ILogger<MemoryAppCache> logger, IHostApplicationLifetime lifetime)
    {
        _cache = cache;
        _logger = logger;

        _cleanupTimer = new Timer(_ => CleanupLocksSafe(), null, _cleanupInterval, _cleanupInterval);

        // Na shutdown: zatrzymaj timer (ładniejsze zamknięcie)
        lifetime.ApplicationStopping.Register(() =>
        {
            try { _cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite); } catch { /* ignore */ }
        });
    }

    public bool TryGet<T>(string key, out T? value)
        => _cache.TryGetValue(key, out value);

    public void Set<T>(string key, T value, TimeSpan ttl)
        => _cache.Set(key, value, ttl);

    public void Remove(string key)
        => _cache.Remove(key);

    public async ValueTask<IAsyncDisposable> AcquireAsync(string key, CancellationToken ct)
    {
        var entry = _locks.GetOrAdd(key, static _ => new LockEntry());

        entry.Touch();
        entry.AddRef();

        await entry.Gate.WaitAsync(ct).ConfigureAwait(false);
        entry.Touch();

        return new Releaser(entry);
    }

    private sealed class Releaser(LockEntry entry) : IAsyncDisposable
    {
        private readonly LockEntry _entry = entry;
        private int _disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return ValueTask.CompletedTask;

            _entry.Gate.Release();
            _entry.Touch();
            _entry.ReleaseRef();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class LockEntry : IDisposable
    {
        public readonly SemaphoreSlim Gate = new(1, 1);

        private int _refCount;
        private long _lastUsedTicks = DateTime.UtcNow.Ticks;

        public void AddRef() => Interlocked.Increment(ref _refCount);
        public void ReleaseRef() => Interlocked.Decrement(ref _refCount);

        public int RefCount => Volatile.Read(ref _refCount);

        public DateTime LastUsedUtc
            => new(Volatile.Read(ref _lastUsedTicks), DateTimeKind.Utc);

        public void Touch()
            => Volatile.Write(ref _lastUsedTicks, DateTime.UtcNow.Ticks);

        public void Dispose()
            => Gate.Dispose();
    }

    private void CleanupLocksSafe()
    {
        try { CleanupLocks(); }
        catch (Exception ex)
        {
            // Cleanup nie może wywalać procesu
            _logger.LogWarning(ex, "MemoryAppCache lock cleanup failed.");
        }
    }

    private void CleanupLocks()
    {
        var now = DateTime.UtcNow;

        foreach (var kv in _locks)
        {
            var key = kv.Key;
            var entry = kv.Value;

            // Nie ruszaj locka jeśli ktoś go używa
            if (entry.RefCount > 0)
                continue;

            // Usuń jeśli nieużywany od dłuższego czasu
            if (now - entry.LastUsedUtc < _lockIdleTtl)
                continue;

            if (_locks.TryRemove(key, out var removed))
            {
                removed.Dispose();
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();

        foreach (var kv in _locks)
        {
            if (_locks.TryRemove(kv.Key, out var removed))
                removed.Dispose();
        }
    }
}

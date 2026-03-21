using Microsoft.Extensions.Logging;
using MyApp.BuildingBlocks.Application.Abstractions.Caching;
using StackExchange.Redis;
using System.Text.Json;

namespace MyApp.BuildingBlocks.Infrastructure.Caching;

public sealed class RedisAppCache(
	IConnectionMultiplexer mux,
	ILogger<RedisAppCache> logger,
	RedisAppCacheOptions options) : IAppCache
{

	private const string ReleaseLockLua = @"
if redis.call('GET', KEYS[1]) == ARGV[1] then
  return redis.call('DEL', KEYS[1])
else
  return 0
end";
	private readonly IDatabase _db = mux.GetDatabase();
	private readonly JsonSerializerOptions _json = options.JsonOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
	private readonly string _prefix = string.IsNullOrWhiteSpace(options.KeyPrefix)
			? "app:"
			: options.KeyPrefix.Trim() + ":";

	public bool TryGet<T>(string key, out T? value)
	{
		value = default;

		var redisKey = CacheKey(key);

		RedisValue raw;
		try
		{
			raw = _db.StringGet(redisKey);
		}
		catch (Exception ex)
		{
			// Cache nie może wywalać requestu – degradacja do “no cache”
			logger.LogWarning(ex, "Redis StringGet failed for key {Key}", redisKey);
			return false;
		}

		if (raw.IsNullOrEmpty)
			return false;

		try
		{
			value = JsonSerializer.Deserialize<T>(raw!, _json);
			return value is not null;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Redis cache deserialize failed for key {Key}", redisKey);
			return false;
		}
	}

	public void Set<T>(string key, T value, TimeSpan ttl)
	{
		var redisKey = CacheKey(key);

		string payload;
		try
		{
			payload = JsonSerializer.Serialize(value, _json);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Redis cache serialize failed for key {Key}", redisKey);
			return;
		}

		try
		{
			_db.StringSet(redisKey, payload, ttl, When.Always, CommandFlags.FireAndForget);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Redis StringSet failed for key {Key}", redisKey);
		}
	}

	public void Remove(string key)
	{
		var redisKey = CacheKey(key);
		try
		{
			_db.KeyDelete(redisKey, CommandFlags.FireAndForget);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Redis KeyDelete failed for key {Key}", redisKey);
		}
	}

	public async ValueTask<IAsyncDisposable> AcquireAsync(string key, CancellationToken ct)
	{
		var lockKey = LockKey(key);
		var token = Guid.NewGuid().ToString("N");

		// TTL locka: musi być > typowego czasu wykonania Query (DB + mapowanie).
		// Dla “dedupe 1s” zwykle starczy 5–10s.
		var ttl = TimeSpan.FromSeconds(10);

		// Backoff: krótki jitter, żeby nie mielić redis w pętli.
		var start = Environment.TickCount64;
		var maxWait = TimeSpan.FromSeconds(3); // ile max czekamy zanim oddamy lock (zwykle wystarczy)

		while (true)
		{
			ct.ThrowIfCancellationRequested();

			bool acquired;
			try
			{
				acquired = await _db.StringSetAsync(lockKey, token, ttl, When.NotExists).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// Jeśli Redis padł, NIE blokujemy requestu – degradujemy do lokalnego “brak locka”.
				logger.LogWarning(ex, "Redis lock acquire failed for {LockKey}. Proceeding without distributed lock.", lockKey);
				return NoopReleaser.Instance;
			}

			if (acquired)
				return new RedisLockReleaser(_db, lockKey, token);

			// lock zajęty – czekaj chwilę
			if (TimeSpan.FromMilliseconds(Environment.TickCount64 - start) > maxWait)
			{
				// Po przekroczeniu limitu: degradacja – nie chcemy wieszać requestów.
				// Zrobisz DB call, ale system nie stanie.
				return NoopReleaser.Instance;
			}

			// jitter 25-60ms
			await Task.Delay(Random.Shared.Next(25, 60), ct).ConfigureAwait(false);
		}
	}

	private RedisKey CacheKey(string key) => (RedisKey)(_prefix + "cache:" + key);
	private RedisKey LockKey(string key) => (RedisKey)(_prefix + "lock:" + key);

	private sealed class RedisLockReleaser(IDatabase db,
		RedisKey lockKey, string token) : IAsyncDisposable
	{
		private int _disposed;

		public ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref _disposed, 1) == 1)
				return ValueTask.CompletedTask;

			try
			{
				db.ScriptEvaluate(
					ReleaseLockLua,
					new RedisKey[] { lockKey },
					new RedisValue[] { token },
					CommandFlags.FireAndForget);
			}
			catch
			{
				// best effort – lock ma TTL
			}

			return ValueTask.CompletedTask;
		}
	}

	private sealed class NoopReleaser : IAsyncDisposable
	{
		public static readonly NoopReleaser Instance = new();
		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

}

namespace MyApp.BuildingBlocks.Application.Abstractions.Caching;

/// <summary>
/// Minimalny cache do Query (storage + keyed lock).
/// Implementacja w Infrastructure (in-memory lub distributed).
/// </summary>
public interface IAppCache
{
	bool TryGet<T>(string key, out T? value);
	void Set<T>(string key, T value, TimeSpan ttl);

	/// <summary>
	/// Keyed async lock – gwarantuje “single flight” dla danego klucza.
	/// </summary>
	ValueTask<IAsyncDisposable> AcquireAsync(string key, CancellationToken ct);

	void Remove(string key);
}

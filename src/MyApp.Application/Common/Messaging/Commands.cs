using MediatR;
using MyApp.Application.Common.Caching;
using MyApp.Domain.Common;

namespace MyApp.Application.Common.Messaging;

/*
 * Ten namespace definiuje „kontrakt” dla requestów MediatR w aplikacji:
 * - rozróżniamy READ (Query) i WRITE (Command)
 * - a UnitOfWorkBehavior na tej podstawie wie, czy ma robić SaveChanges / transakcję.
 *
 * WAŻNE (EF Core + EnableRetryOnFailure / ExecutionStrategy):
 * - gdy używasz retry na SQL Server (EnableRetryOnFailure), transakcje MUSZĄ być uruchamiane
 *   wewnątrz execution strategy (CreateExecutionStrategy().ExecuteAsync(...)).
 * - dlatego nie wolno robić „ręcznego” BeginTransaction w handlerach w przypadkowych miejscach.
 * - jeśli potrzebujesz transakcji obejmującej CAŁĄ komendę (wiele zapisów, atomowość),
 *   oznacz komendę jako ITransactionalCommand, a UnitOfWorkBehavior wykona ją poprawnie
 *   (wrap w ExecuteInTransactionAsync).
 *
 * W skrócie:
 * - Query => żadnych zapisów do DB (brak SaveChanges w pipeline)
 * - Command => pipeline zrobi SaveChanges na końcu (jeśli handler zwróci success)
 * - TransactionalCommand => pipeline zrobi to samo, ale całość (handler + zapisy) w retry-safe transakcji
 * - SkipUnitOfWorkBehavior => pipeline NIC nie zrobi (ty bierzesz odpowiedzialność za zapisy/tx)
 */

// ------------------------------
// READ (Queries)
// ------------------------------

/// <summary>
/// Marker (pusty interfejs) do odróżnienia zapytań READ od komend WRITE.
/// UnitOfWorkBehavior używa tego rozróżnienia, żeby NIE robić SaveChanges dla query.
/// </summary>
public interface IQueryMarker { }

/// <summary>
/// Query = operacja READ.
/// Zasada: query nie modyfikuje bazy (brak SaveChanges).
/// Zwraca MessageResult<TResponse>, czyli albo sukces + dane, albo błędy (ErrorData).
/// </summary>
public interface IQuery<TResponse> : IRequest<MessageResult<TResponse>>, IQueryMarker { }

// ------------------------------
// READ (Queries) - Caching (optional)
// ------------------------------

/// <summary>
/// Cacheable Query = operacja READ, która może być cache’owana (Memory/Redis zależnie od DI).
///
/// Po co:
/// - bursty (np. UI po loginie robi 2x ten sam GET w 1 sekundę)
/// - thundering herd / cache stampede
///
/// Jak działa:
/// - QueryCachingBehavior sprawdza cache po kluczu
/// - jeśli miss -> robi keyed lock (single-flight)
/// - cache’uje Success na Ttl
/// - opcjonalnie cache’uje NotFound na krótko (NotFoundTtl)
///
/// UWAGA:
/// - CacheKey NIE może zawierać access tokena (rotuje); używamy sub/tenant w BuildKey.
/// - Dla odpowiedzi zależnych od usera używaj Scope=User (lub Tenant).
/// </summary>
public interface ICacheableQuery : IQueryMarker
{
    /// <summary>Unikalny fragment klucza zależny od parametrów zapytania.</summary>
    string CacheKey { get; }

    /// <summary>TTL dla Success (typowo 0.5–2s na deduplikację burstów).</summary>
    TimeSpan Ttl { get; }

    /// <summary>Zakres cache (Global/User/Tenant). Dla Keycloak zwykle User/Tenant.</summary>
    CacheScope Scope { get; }

    /// <summary>Czy cache’ować NotFound (krótko), żeby nie bić DB “gorącymi” 404.</summary>
    bool CacheNotFound { get; }

    /// <summary>TTL dla NotFound (np. 250–800ms).</summary>
    TimeSpan NotFoundTtl { get; }

    bool VaryByRoles { get; }
}

public interface ICacheableQuery<TResponse> : IQuery<TResponse>, ICacheableQuery { }

// ------------------------------
// WRITE (Commands)
// ------------------------------

/// <summary>
/// Marker (pusty interfejs) do odróżnienia komend WRITE od query.
/// UnitOfWorkBehavior traktuje wszystkie ICommandMarker jako „zapisujące”
/// i wtedy automatycznie robi SaveChanges, jeśli handler zwróci sukces.
/// </summary>
public interface ICommandMarker { }

/// <summary>
/// Command = operacja WRITE (bez wartości w odpowiedzi).
///
/// Jak działa z UnitOfWorkBehavior:
/// - Handler wykonuje zmiany na DbContext (Add/Update/Remove), ale NIE robi commit/rollback.
/// - Po zakończeniu handlera pipeline:
///   - jeśli MessageResult = Success -> wywoła uow.SaveChangesAsync()
///   - jeśli MessageResult = Fail    -> NIE zapisze nic i wyczyści kolejkę post-save (uow.ClearPostSaveQueue()).
///
/// WAŻNE przy EF Core + EnableRetryOnFailure (retry na DB):
/// - Dla ICommand (bez ITransactionalCommand) pipeline NIE uruchamia transakcji.
/// - Nie rób w handlerze BeginTransaction/Commit/Rollback.
///   To może kolidować z execution strategy (retry) i łamie założenia pipeline’u.
/// - Jeśli potrzebujesz atomowości (wiele zapisów jako jedna całość) -> użyj ITransactionalCommand.
/// - Jeśli musisz ręcznie sterować zapisami/tx -> oznacz komendę ISkipUnitOfWorkBehavior i wtedy
///   TY odpowiadasz za całość (najlepiej nadal przez uow.ExecuteInTransactionAsync, nie manual BeginTransaction).
/// </summary>
public interface ICommand : IRequest<MessageResult>, ICommandMarker { }

/// <summary>
/// Command = operacja WRITE (z wartością w odpowiedzi).
///
/// Jak działa z UnitOfWorkBehavior:
/// - Handler zwraca MessageResult<TResponse> (np. zawiera Id utworzonego rekordu).
/// - Pipeline zachowuje się identycznie jak dla ICommand:
///   - Success -> uow.SaveChangesAsync()
///   - Fail    -> brak zapisu + czyszczenie post-save.
///
/// WAŻNE przy EF Core + EnableRetryOnFailure (retry na DB):
/// - Tak samo: NIE rób w handlerze BeginTransaction/Commit/Rollback dla zwykłego ICommand<T>.
/// - Jeśli w use-case:
///   - robisz kilka zapisów,
///   - potrzebujesz ID po pierwszym SaveChanges,
///   - albo chcesz 100% atomowości,
///   to oznacz request jako ITransactionalCommand, żeby całość poszła w retry-safe transakcji
/// (CreateExecutionStrategy().ExecuteAsync + BeginTx + SaveChanges + Commit).
/// </summary>
public interface ICommand<TResponse> : IRequest<MessageResult<TResponse>>, ICommandMarker { }


/// <summary>
/// Komenda wymagająca transakcji.
/// Używaj gdy:
/// - operacja ma być atomowa (np. insert do kilku tabel),
/// - robisz kilka SaveChanges w jednym use-case (np. chcesz ID po pierwszym SaveChanges),
/// - i rollback ma cofnąć CAŁOŚĆ, jeśli coś się nie uda.
///
/// Dlaczego to ważne przy retry?
/// - przy EnableRetryOnFailure EF może powtórzyć całą operację po transient error.
/// - transakcja musi wtedy być utworzona w execution strategy (nie ręcznie w handlerze).
/// - UnitOfWorkBehavior zrobi to za Ciebie: uow.ExecuteInTransactionAsync(...).
///
/// Czego NIE robić:
/// - nie odpalaj ręcznie BeginTransactionAsync w handlerach, jeśli masz EnableRetryOnFailure.
/// - nie rób side-effectów (HTTP, publish eventów) „w środku transakcji” – do tego jest outbox/worker.
/// </summary>
public interface ITransactionalCommand : ICommandMarker { }

/// <summary>
/// „Wyłącz automatyczny UnitOfWorkBehavior” dla tej komendy.
/// Używaj TYLKO gdy naprawdę musisz sam zarządzić zapisami/tx, np.:
/// - import danych (bulk) z wieloma SaveChanges i ręcznym sterowaniem,
/// - bardzo specyficzne operacje na DbContext (np. raw SQL) i sam decydujesz kiedy zapisać.
///
/// Wtedy pipeline:
/// - nie zrobi SaveChanges,
/// - nie opakuje w transakcję,
/// - nic nie posprząta za Ciebie.
///
/// WAŻNE:
/// - bierzesz 100% odpowiedzialności za spójność danych i poprawne podejście do retry.
/// - jeśli masz EnableRetryOnFailure i potrzebujesz transakcji, to nadal powinieneś użyć
///   uow.ExecuteInTransactionAsync(...) (retry-safe), a nie własnego BeginTransaction.
/// </summary>
public interface ISkipUnitOfWorkBehavior : ICommandMarker { }

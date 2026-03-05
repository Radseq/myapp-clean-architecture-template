using MediatR;
using MyApp.BuildingBlocks.Application.Abstractions.Persistence;
using MyApp.BuildingBlocks.Application.Common.Messaging;
using MyApp.BuildingBlocks.Domain.Common;
using System.Collections.Concurrent;
using System.Reflection;

namespace MyApp.BuildingBlocks.Application.Common.Behaviors;

public sealed class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWorkResolver uowResolver)
	: IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull
	where TResponse : MessageResult
{
	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken)
	{
		if (request is not ICommandMarker)
			return await next(cancellationToken);

		if (request is ISkipUnitOfWorkBehavior)
			return await next(cancellationToken);

		var uow = uowResolver.ResolveFor(typeof(TRequest));

		var useTx = request is ITransactionalCommand;

		if (!useTx)
			return await HandleNoTx(uow, next, cancellationToken);

		return await HandleWithTx(uow, next, cancellationToken);
	}

	private static async Task<TResponse> HandleNoTx(
		IUnitOfWork uow, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
	{
		var response = await next(ct);

		if (response.HasFailed)
		{
			uow.ClearPostSaveQueue();
			return response;
		}

		var save = await uow.SaveChangesAsync(ct);
		if (save.HasFailed)
			return MessageResultFactory<TResponse>.Failure(save.Errors);

		return response;
	}

	private static Task<TResponse> HandleWithTx(
		IUnitOfWork uow, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
		=> uow.ExecuteInTransactionAsync(async c =>
		{
			var response = await next(c);

			if (response.HasFailed)
			{
				uow.ClearPostSaveQueue();
				return response;
			}

			var save = await uow.SaveChangesAsync(c);
			if (save.HasFailed)
				return MessageResultFactory<TResponse>.Failure(save.Errors);

			return response;
		}, ct);

	private static class MessageResultFactory<T> where T : MessageResult
	{
		private static readonly ConcurrentDictionary<Type, Func<IReadOnlyList<ErrorData>, T>> Cache = new();

		public static T Failure(IReadOnlyList<ErrorData> errors)
			=> Cache.GetOrAdd(typeof(T), Build)(errors);

		private static Func<IReadOnlyList<ErrorData>, T> Build(Type t)
		{
			// case 1: MessageResult
			if (t == typeof(MessageResult))
			{
				return errs => (T)(object)MessageResult.Fail(errs);
			}

			// case 2: MessageResult<TValue>
			if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(MessageResult<>))
			{
				// szukamy: public static MessageResult<TValue> Failure(IEnumerable<ErrorData> errors)
				var mi = t.GetMethods(BindingFlags.Public | BindingFlags.Static)
					.FirstOrDefault(m =>
						m.Name == nameof(MessageResult.Fail) &&
						m.GetParameters().Length == 1 &&
						typeof(IEnumerable<ErrorData>).IsAssignableFrom(m.GetParameters()[0].ParameterType));

				if (mi is null)
					throw new InvalidOperationException($"No Failure(IEnumerable<ErrorData>) on {t.FullName}");

				return errs => (T)mi.Invoke(null, [errs])!;
			}

			throw new InvalidOperationException($"Unsupported response type: {t.FullName}");
		}
	}
}
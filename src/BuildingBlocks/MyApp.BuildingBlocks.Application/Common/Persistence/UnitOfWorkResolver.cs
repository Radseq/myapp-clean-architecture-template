using Microsoft.Extensions.DependencyInjection;
using MyApp.BuildingBlocks.Application.Abstractions.Persistence;

namespace MyApp.BuildingBlocks.Application.Common.Persistence;

internal sealed class UnitOfWorkResolver(
	IServiceProvider sp,
	IEnumerable<IUnitOfWorkRoute> routes)
	: IUnitOfWorkResolver
{
	public IUnitOfWork ResolveFor(Type requestType)
	{
		var asm = requestType.Assembly;

		var route = routes.FirstOrDefault(r => r.RequestsAssembly == asm);
		if (route is null)
			throw new InvalidOperationException($"No UnitOfWork route registered for assembly: {asm.GetName().Name}");

		var uow = sp.GetRequiredService(route.UnitOfWorkServiceType);
		return (IUnitOfWork)uow;
	}
}
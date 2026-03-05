using System.Reflection;

namespace MyApp.BuildingBlocks.Application.Abstractions.Persistence;

public interface IUnitOfWorkRoute
{
	Assembly RequestsAssembly { get; }
	Type UnitOfWorkServiceType { get; } // np. typeof(IOrdersUnitOfWork)
}

public sealed record UnitOfWorkRoute(Assembly RequestsAssembly, 
	Type UnitOfWorkServiceType) : IUnitOfWorkRoute;
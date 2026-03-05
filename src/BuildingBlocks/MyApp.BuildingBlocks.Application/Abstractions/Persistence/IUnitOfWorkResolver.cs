namespace MyApp.BuildingBlocks.Application.Abstractions.Persistence;

public interface IUnitOfWorkResolver
{
	IUnitOfWork ResolveFor(Type requestType);
}
namespace MyApp.BuildingBlocks.Infrastructure.Outbox;

public interface IOutboxModuleOptions<TModule>
{
	string OptionsName { get; }
}
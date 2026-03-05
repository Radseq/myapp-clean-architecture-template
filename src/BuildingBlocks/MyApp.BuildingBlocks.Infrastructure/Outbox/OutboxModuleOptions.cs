namespace MyApp.BuildingBlocks.Infrastructure.Outbox;

internal sealed class OutboxModuleOptions<TModule>(string optionsName)
	: IOutboxModuleOptions<TModule>
{
	public string OptionsName { get; } = optionsName;
}
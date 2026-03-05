namespace MyApp.IntegrationContracts.Outbox;

// Markery – NIE mają logiki. To tylko "token" do typowania DI.
public static class OutboxOwners
{
	public sealed class Orders { }
	public sealed class Payments { }
	public sealed class Transport { }
}
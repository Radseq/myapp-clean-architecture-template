namespace MyApp.BuildingBlocks.Infrastructure.Outbox.Storage;

public static class OutboxStatusCodes
{
	public const byte Pending = 0;
	public const byte Processing = 1;
	public const byte Done = 2;
	public const byte Failed = 3;
	public const byte Dead = 4;
}
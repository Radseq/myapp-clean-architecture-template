namespace MyApp.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst.Enums;

public enum OutboxStatus : byte
{
    Pending = 0,
    Processing = 1,
    Done = 2,
    Failed = 3,
    Dead = 4
}

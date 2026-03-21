namespace MyApp.BuildingBlocks.Application.Abstractions.Observability;

public interface ICorrelationContext
{
	string? CorrelationId { get; set; }
}
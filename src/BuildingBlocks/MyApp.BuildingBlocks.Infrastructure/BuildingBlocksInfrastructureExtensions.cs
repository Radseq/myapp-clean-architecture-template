using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MyApp.BuildingBlocks.Infrastructure;

public static class BuildingBlocksInfrastructureExtensions
{
	public static IServiceCollection AddBuildingBlocksInfrastructure(
		this IServiceCollection services,
		IConfiguration cfg,
		IHostEnvironment env)
		=> services.AddInfrastructure(cfg, env);
}
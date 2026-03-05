using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using MyApp.Modules.Orders.Application.Abstractions.Persistence;
using MyApp.Modules.Orders.Application.Features.GetOrderById;
using MyApp.Modules.Orders.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst;

namespace MyApp.Modules.Orders.Infrastructure.Repositories;

internal sealed class OrderReadRepository(OrdersDbContext db, IMapper mapper) : IOrderReadRepository
{
	private readonly IConfigurationProvider _mapperConfig = mapper.ConfigurationProvider;

	public Task<OrderDto?> GetByIdAsync(int id, CancellationToken ct)
	{
		return db.Orders
			.AsNoTracking()
			.Where(o => o.Id == id)
			.ProjectTo<OrderDto>(_mapperConfig)
			.FirstOrDefaultAsync(ct);
	}
}

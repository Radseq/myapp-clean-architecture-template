using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using MyApp.Application.Abstractions.Persistence;
using MyApp.Application.Orders.Dtos;
using MyApp.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst;

namespace MyApp.Infrastructure.Persistence.Repositories;

internal sealed class OrderReadRepository(AppDbContext db, IMapper mapper) : IOrderReadRepository
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

using FluentValidation;

namespace MyApp.Modules.Orders.Application.Features.CreateOrderAndDispatchTransport;

public sealed class Validator : AbstractValidator<Command>
{
	public Validator()
	{
		RuleFor(x => x.CustomerId).GreaterThan(0);

		RuleFor(x => x.Items)
			.NotNull()
			.Must(x => x.Count > 0).WithMessage("At least one item is required.");

		RuleForEach(x => x.Items).ChildRules(item =>
		{
			item.RuleFor(i => i.ProductId).GreaterThan(0);
			item.RuleFor(i => i.UnitPrice).GreaterThan(0m);
			item.RuleFor(i => i.Quantity).GreaterThan(0);
		});
	}
}

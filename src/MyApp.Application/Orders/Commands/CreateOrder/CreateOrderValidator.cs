using FluentValidation;

namespace MyApp.Application.Orders.Commands.CreateOrder;

public sealed class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
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

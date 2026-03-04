using MyApp.BuildingBlocks.Domain.Common;

namespace MyApp.Modules.Orders.Domain.Orders;

internal static class OrderErrors
{
    // Validation (400)
    public static readonly ErrorData CustomerInvalid = new(
        code: 5201,
        key: "errors.order.customer_invalid",
        description: "CustomerId must be > 0",
        kind: ErrorKind.Validation);

    public static readonly ErrorData ItemsEmpty = new(
        code: 5202,
        key: "errors.order.items_empty",
        description: "Order must have at least one item",
        kind: ErrorKind.Validation);

    public static readonly ErrorData QuantityTooHigh = new(
        code: 5207,
        key: "errors.order.quantity_too_high",
        description: "Quantity per item cannot exceed limit",
        kind: ErrorKind.Validation);

    public static readonly ErrorData TotalInvalid = new(
        code: 5208,
        key: "errors.order.total_invalid",
        description: "Total must be > 0",
        kind: ErrorKind.Validation);

    public static readonly ErrorData IdInvalid = new(
        code: 5209,
        key: "errors.order.id_invalid",
        description: "Id must be > 0",
        kind: ErrorKind.Validation);

    // Conflict (409) – reguły stanu / operacje niedozwolone
    public static readonly ErrorData ModifyNotAllowed = new(
        code: 5203,
        key: "errors.order.modify_not_allowed",
        description: "Cannot modify order in current state",
        kind: ErrorKind.Conflict);

    public static readonly ErrorData ConfirmInvalidState = new(
        code: 5205,
        key: "errors.order.confirm_invalid_state",
        description: "Cannot confirm order in current state",
        kind: ErrorKind.Conflict);

    public static readonly ErrorData CancelNotAllowed = new(
        code: 5206,
        key: "errors.order.cancel_not_allowed",
        description: "Cannot cancel order in current state",
        kind: ErrorKind.Conflict);

    // NotFound (404)
    public static readonly ErrorData ItemNotFound = new(
        code: 5204,
        key: "errors.order.item_not_found",
        description: "Item not found",
        kind: ErrorKind.NotFound);

    // OrderItem (validation)
    public static readonly ErrorData ProductIdInvalid = new(
        code: 5301,
        key: "errors.order_item.product_id_invalid",
        description: "ProductId must be > 0",
        kind: ErrorKind.Validation);

    public static readonly ErrorData UnitPriceInvalid = new(
        code: 5302,
        key: "errors.order_item.unit_price_invalid",
        description: "UnitPrice must be > 0",
        kind: ErrorKind.Validation);

    public static readonly ErrorData QuantityInvalid = new(
        code: 5303,
        key: "errors.order_item.quantity_invalid",
        description: "Quantity must be > 0",
        kind: ErrorKind.Validation);

    public static readonly ErrorData DeltaInvalid = new(
        code: 5304,
        key: "errors.order_item.delta_invalid",
        description: "Delta must be > 0",
        kind: ErrorKind.Validation);

    public static readonly ErrorData QuantityTooLow = new(
        code: 5305,
        key: "errors.order_item.quantity_too_low",
        description: "Quantity cannot go <= 0",
        kind: ErrorKind.Validation);
}
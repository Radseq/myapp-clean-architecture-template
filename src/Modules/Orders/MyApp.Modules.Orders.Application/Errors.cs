using MyApp.BuildingBlocks.Domain.Common;

namespace MyApp.Modules.Orders.Application;

/// <summary>
/// Katalog błędów aplikacji. Stabilny kontrakt: Code + Key + Args.
/// Description możesz trzymać jako PL fallback (tymczasowo).
///
/// UWAGA: Kind jest używany do mapowania na HTTP (w Presentation),
/// ale jest JsonIgnore => NIE wypływa do klienta.
/// </summary>
public static class Errors
{
    public static class Orders
    {
        public static ErrorData NotFound(int id) => new(
            code: 2001,
            key: "errors.orders.not_found",
            description: "Nie znaleziono zamówienia.", // fallback PL
            args: [id],
            kind: ErrorKind.NotFound);

        public static readonly ErrorData EmptyItems = new(
            code: 2002,
            key: "errors.orders.empty_items",
            description: "Zamówienie musi zawierać co najmniej jedną pozycję.",
            kind: ErrorKind.Validation);
    }

    public static class Customers
    {
        public static ErrorData NotFound(int id) => new(
            code: 2101,
            key: "errors.customers.not_found",
            description: "Nie znaleziono klienta.",
            args: [id],
            kind: ErrorKind.NotFound);
    }
}
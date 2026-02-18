using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyApp.Api.Common;
using MyApp.Application.Orders.Commands.CreateOrder;
using MyApp.Application.Orders.Queries.GetOrderById;
using MyApp.Shared;
using Swashbuckle.AspNetCore.Annotations;

namespace MyApp.Api.Controllers.V1;

[ApiController]
[Route("api/[controller]/[action]")]
public sealed class OrdersController(IMediator mediator, IMapper mapper) : ControllerBase
{
    /// <summary>
    /// Creates an order and dispatches a transport request.
    /// </summary>
    /// <param name="dto">Create order request DTO.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created on success; ApiProblemDetails on failure.</returns>
    [HttpPost("create-with-transport")]
    [Consumes("application/json")]
    [SwaggerOperation(
        Summary = "Create order with transport",
        Description = "Creates a new order and dispatches it to the external transport process.",
        OperationId = "Orders_CreateWithTransport")]
    [ProducesResponseType(typeof(ApiResponse<CreateOrderAndDispatchTransportResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateWithTransport(
        [FromBody] CreateOrderAndDispatchTransportRequestDto dto,
        CancellationToken ct)
    {
        return await this.SendCreatedAtAction(
            mediator,
            request: mapper.Map<CreateOrderAndDispatchTransportCommand>(dto),
            mapDto: mapper.Map<CreateOrderAndDispatchTransportResponseDto>,
            getByIdActionName: nameof(GetById),
            routeValuesFactory: resp => this.WithRequestedApiVersion(new { id = resp.OrderId }),
            ct);
    }

    /// <summary>
    /// Creates an order.
    /// </summary>
    /// <remarks>
    /// On pure success returns 204 NoContent.
    /// If operation is partially successful (warnings present), returns 200 OK with ApiResponse(null, warnings).
    /// </remarks>
    /// <param name="dto">Create order request DTO.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("create")]
    [Consumes("application/json")]
    [SwaggerOperation(
        Summary = "Create order",
        Description = "Creates a new order.",
        OperationId = "Orders_Create")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)] // partial success => warnings
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrderAndDispatchTransportRequestDto dto,
        CancellationToken ct)
    {
        return await this.SendNoContent(
            mediator,
            request: mapper.Map<CreateOrderCommand>(dto),
            ct);
    }

    /// <summary>
    /// Gets an order by its identifier.
    /// </summary>
    /// <param name="id">Order identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with order; 404 if not found.</returns>
    [HttpGet("{id:int}")]
    [SwaggerOperation(
        Summary = "Get order by id",
        Description = "Returns the order details for the provided identifier.",
        OperationId = "Orders_GetById")]
    [ProducesResponseType(typeof(ApiResponse<OrderResponseModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        return await this.SendOk(
            mediator,
            new GetOrderByIdQuery(id),
            map: mapper.Map<OrderResponseModel>,
            ct);
    }
}

using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyApp.Api.Common;
using Swashbuckle.AspNetCore.Annotations;

namespace MyApp.Api.Controllers.V1;

[ApiController]
[Route("api/[controller]/[action]")]
public sealed class TransportOrdersController(IMediator mediator) : ControllerBase
{

	/// <summary>
	/// Resends a transport order to the external Transport API.
	/// </summary>
	/// <param name="id">Transport order identifier.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>200 OK on success; ProblemDetails on failure.</returns>
	//[HttpPost("{id:int}/resend")]
	//[SwaggerOperation(
	//	Summary = "Resend transport order",
	//	Description = "Triggers a resend of the transport order to the external transport system.",
	//	OperationId = "TransportOrders_Resend"
	//)]
	//[ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
	//[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	//[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	//[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
	//public async Task<IActionResult> Resend([FromRoute] int id, CancellationToken ct)
	//{
	//	return await this.SendNoContent(
	//		mediator,
	//		new ResendTransportOrderCommand(id),
	//		ct);
	//}
}

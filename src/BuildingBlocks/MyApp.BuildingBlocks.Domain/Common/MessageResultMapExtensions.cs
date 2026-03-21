namespace MyApp.BuildingBlocks.Domain.Common;

public static class MessageResultMapExtensions
{
	public static MessageResult<TOut> Map<TIn, TOut>(
		this MessageResult<TIn> r,
		Func<TIn?, TOut> map)
	{
		if (r.HasFailed)
			return MessageResult.Fail<TOut>(r.Errors)
				.WithDiagnostics(r.Diagnostics);

		var mapped = map(r.Value);

		// Partial wynika z warnings automatycznie
		return MessageResult.Ok(mapped, r.Warnings.ToArray())
			.WithDiagnostics(r.Diagnostics);
	}
}
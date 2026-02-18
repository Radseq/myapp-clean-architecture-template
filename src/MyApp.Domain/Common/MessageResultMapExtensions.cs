namespace MyApp.Domain.Common;

public static class MessageResultMapExtensions
{
	public static MessageResult<TOut> Map<TIn, TOut>(
		this MessageResult<TIn> r,
		Func<TIn?, TOut> map)
	{
		if (r.HasFailed)
			return MessageResult.Fail<TOut>(r.Errors);

		var mapped = map(r.Value);

		return r.IsPartial
			? MessageResult.Partial(mapped, r.Warnings.ToArray())
			: MessageResult.Ok(mapped);
	}
}

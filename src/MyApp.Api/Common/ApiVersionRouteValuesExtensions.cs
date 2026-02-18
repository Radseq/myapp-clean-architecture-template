using Microsoft.AspNetCore.Mvc;

namespace MyApp.Api.Common;

public static class ApiVersionRouteValuesExtensions
{
	public static object WithRequestedApiVersion(this ControllerBase controller, object routeValues)
	{
		var version = controller.HttpContext.GetRequestedApiVersion()?.ToString();
		if (string.IsNullOrWhiteSpace(version))
			return routeValues;

		var dict = new RouteValueDictionary(routeValues)
		{
			["version"] = version
		};

		return dict;
	}
}

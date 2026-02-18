using FluentValidation;
using FluentValidation.Results;
using MediatR;
using MyApp.Application.Common;
using MyApp.Domain.Common;
using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace MyApp.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly Func<IEnumerable<ErrorData>, TResponse> FailFactory = CreateFailFactory();

    private static Func<IEnumerable<ErrorData>, TResponse> CreateFailFactory()
    {
        if (typeof(TResponse) == typeof(MessageResult))
            return errors => (TResponse)(object)MessageResult.Fail(errors);

        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(MessageResult<>))
        {
            var tArg = typeof(TResponse).GetGenericArguments()[0];
            var closedType = typeof(MessageResult<>).MakeGenericType(tArg);

            var mi = closedType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == nameof(MessageResult.Fail))
                .Where(m => m.GetParameters().Length == 1)
                .Where(m => m.GetParameters()[0].ParameterType == typeof(IEnumerable<ErrorData>))
                .SingleOrDefault();

            if (mi is null)
                throw new InvalidOperationException(
                    $"Cannot find {closedType.FullName}.Fail(IEnumerable<ErrorData>) overload.");

            var p = Expression.Parameter(typeof(IEnumerable<ErrorData>), "errors");
            var call = Expression.Call(mi, p);
            var cast = Expression.Convert(call, typeof(TResponse));

            return Expression.Lambda<Func<IEnumerable<ErrorData>, TResponse>>(cast, p).Compile();
        }

        throw new InvalidOperationException(
            $"ValidationBehavior supports only MessageResult / MessageResult<T>, got: {typeof(TResponse).FullName}");
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(e => e is not null)
            .ToArray();

        if (failures.Length == 0)
            return await next(cancellationToken);

        var details = failures
            .Select(MapFailure)
            .Distinct(ValidationErrorFingerprintComparer.Instance)
            .ToArray();

        var main = Errors.Validation.Failed.WithExtended(details);
        return FailFactory([main]);
    }

    private static ErrorData MapFailure(ValidationFailure f)
    {
        var field = f.PropertyName;
        var code = (f.ErrorCode ?? string.Empty).Trim();

        // FV defaultuje ErrorCode do nazwy validatora (np. "GreaterThanValidator").
        // Nie u¿ywamy ErrorMessage -> stabilny kontrakt + lokalizacja po naszej stronie.
        return code switch
        {
            "NotEmptyValidator" or "NotNullValidator" or "EmptyValidator" =>
                Errors.Validation.Required(field),

            "GreaterThanValidator" =>
                Errors.Validation.GreaterThan(field, GetPlaceholder(f, "ComparisonValue")),
            "GreaterThanOrEqualValidator" =>
                Errors.Validation.GreaterOrEqual(field, GetPlaceholder(f, "ComparisonValue")),
            "LessThanValidator" =>
                Errors.Validation.LessThan(field, GetPlaceholder(f, "ComparisonValue")),
            "LessThanOrEqualValidator" =>
                Errors.Validation.LessOrEqual(field, GetPlaceholder(f, "ComparisonValue")),

            "InclusiveBetweenValidator" or "ExclusiveBetweenValidator" =>
                Errors.Validation.Between(field,
                    GetPlaceholder(f, "From") ?? GetPlaceholder(f, "MinValue"),
                    GetPlaceholder(f, "To") ?? GetPlaceholder(f, "MaxValue")),

            "LengthValidator" =>
                Errors.Validation.LengthBetween(field,
                    GetPlaceholder(f, "MinLength"),
                    GetPlaceholder(f, "MaxLength")),
            "MinimumLengthValidator" =>
                Errors.Validation.MinLength(field, GetPlaceholder(f, "MinLength") ?? GetPlaceholder(f, "Length")),
            "MaximumLengthValidator" =>
                Errors.Validation.MaxLength(field, GetPlaceholder(f, "MaxLength") ?? GetPlaceholder(f, "Length")),
            "ExactLengthValidator" =>
                Errors.Validation.LengthBetween(field,
                    GetPlaceholder(f, "MaxLength") ?? GetPlaceholder(f, "Length"),
                    GetPlaceholder(f, "MaxLength") ?? GetPlaceholder(f, "Length")),

            "EmailValidator" =>
                Errors.Validation.Email(field),

            "ScalePrecisionValidator" or "PrecisionScaleValidator" =>
                Errors.Validation.PrecisionScale(field,
                    GetPlaceholder(f, "ExpectedPrecision") ?? GetPlaceholder(f, "Precision"),
                    GetPlaceholder(f, "ExpectedScale") ?? GetPlaceholder(f, "Scale")),

            _ => Errors.Validation.UnknownRule(field, string.IsNullOrWhiteSpace(code) ? null : code)
        };
    }

    private static object? GetPlaceholder(ValidationFailure f, string name)
    {
        if (f.FormattedMessagePlaceholderValues is null)
            return null;

        return f.FormattedMessagePlaceholderValues.TryGetValue(name, out var val) ? val : null;
    }

    private sealed class ValidationErrorFingerprintComparer : IEqualityComparer<ErrorData>
    {
        public static readonly ValidationErrorFingerprintComparer Instance = new();

        public bool Equals(ErrorData? x, ErrorData? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            if (x.Code != y.Code) return false;
            if (!string.Equals(x.Key, y.Key, StringComparison.Ordinal)) return false;
            return ArgsEqual(x.Args, y.Args);
        }

        public int GetHashCode(ErrorData obj)
        {
            var hc = new HashCode();
            hc.Add(obj.Code);
            hc.Add(obj.Key, StringComparer.Ordinal);
            foreach (var a in obj.Args)
                hc.Add(NormalizeArg(a), StringComparer.Ordinal);
            return hc.ToHashCode();
        }

        private static bool ArgsEqual(object?[]? a, object?[]? b)
        {
            a ??= Array.Empty<object?>();
            b ??= Array.Empty<object?>();
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (!string.Equals(NormalizeArg(a[i]), NormalizeArg(b[i]), StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static string NormalizeArg(object? v)
        {
            if (v is null) return "<null>";
            if (v is string s) return s;
            if (v is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture) ?? v.ToString()!;
            if (v is IEnumerable e and not string)
                return string.Join(",", e.Cast<object?>().Select(NormalizeArg));
            return v.ToString() ?? "<null>";
        }
    }
}

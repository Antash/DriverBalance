namespace DriverBalance.Web;

public sealed record ErrorsResponse(IReadOnlyList<string> Errors);

/// <summary>The vehicle does not exist for the current user (or belongs to someone else).</summary>
public sealed class VehicleNotFoundException(long id) : Exception($"Vehicle {id} not found.");

public sealed class ApiValidationException(IReadOnlyList<string> errors) : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
    public ApiValidationException(string error) : this([error]) { }
}

/// <summary>Maps domain exceptions to 404 and 400 {errors}.</summary>
public sealed class ApiErrorFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (VehicleNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ApiValidationException e)
        {
            return Results.BadRequest(new ErrorsResponse(e.Errors));
        }
    }
}


using FluentValidation;

namespace Api;

public class ValidationFilter<TRequest>: IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var model = context.Arguments.OfType<TRequest>().First();

        var validator = context.HttpContext.RequestServices.GetService<IValidator<TRequest>>();

        if (validator == null) return await next.Invoke(context);
        
        var validationResult = await validator.ValidateAsync(model);
        
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }
        
        return await next.Invoke(context);
    }
}
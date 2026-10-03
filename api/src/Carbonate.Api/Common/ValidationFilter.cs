using Carbonate.Application.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Carbonate.Api.Common;

/// <summary>
/// Runs the FluentValidation validator for every action argument that has one, and reports all field
/// errors together as a 400. Every request DTO gets a validator using allowlists (plan section 5).
/// </summary>
internal sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, List<string>>();

        foreach (var argument in context.ActionArguments.Values.Where(a => a is not null))
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(argument!.GetType());
            if (services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument));
            foreach (var failure in result.Errors)
            {
                var field = char.ToLowerInvariant(failure.PropertyName[0]) + failure.PropertyName[1..];
                if (!errors.TryGetValue(field, out var messages))
                {
                    errors[field] = messages = [];
                }

                messages.Add(failure.ErrorMessage);
            }
        }

        if (errors.Count > 0)
        {
            throw ProblemException.Validation(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }

        await next();
    }
}

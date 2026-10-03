using Carbonate.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Common;

/// <summary>Turns a <see cref="ProblemException"/> into RFC 7807 problem details with the right status.</summary>
internal sealed class ProblemExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is not ProblemException problem)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status;

        ProblemDetails body = problem.Errors.Count > 0
            ? new ValidationProblemDetails(problem.Errors)
            : new ProblemDetails();
        body.Status = problem.Status;
        body.Type = problem.Type;
        body.Title = problem.Title;
        body.Detail = problem.Message;
        foreach (var (key, value) in problem.Extensions)
        {
            body.Extensions[key] = value;
        }

        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = body,
            Exception = exception,
        });
    }
}

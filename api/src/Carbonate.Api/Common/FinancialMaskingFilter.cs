using Carbonate.Application.Masking;
using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Carbonate.Api.Common;

/// <summary>
/// Safety net: masks every object a controller returns, so an endpoint that forgets to mask in its
/// service still cannot leak a financial field (plan section 7.3, step 4).
/// </summary>
internal sealed class FinancialMaskingFilter(IFinancialMasker masker, ICurrentUser currentUser) : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: not null } result)
        {
            masker.Mask(result.Value, currentUser);
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}

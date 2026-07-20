using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BillFoundry.Web.Hosting;

internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "An unhandled exception occurred.");

        if (AcceptsHtml(httpContext.Request) && !AcceptsJson(httpContext.Request))
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1"
            }
        }).ConfigureAwait(false);
    }

    private static bool AcceptsHtml(HttpRequest request) =>
        request.Headers.Accept.Any(value =>
            value is not null && value.Contains("text/html", StringComparison.OrdinalIgnoreCase));

    private static bool AcceptsJson(HttpRequest request) =>
        request.Headers.Accept.Any(value =>
            value is not null && value.Contains("application/json", StringComparison.OrdinalIgnoreCase));
}

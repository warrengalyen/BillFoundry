namespace BillFoundry.Web.Hosting;

internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task Invoke(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var httpContext = (HttpContext)state;
            IHeaderDictionary headers = httpContext.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers["Content-Security-Policy"] = BuildContentSecurityPolicy(httpContext.Request.IsHttps);

            return Task.CompletedTask;
        }, context);

        return next(context);
    }

    internal static string BuildContentSecurityPolicy(bool https)
    {
        // Blazor Web App emits an importmap and inline boot script, and Interactive Server
        // uses WebSockets. style-src includes 'unsafe-inline' because report bars set width
        // with element styles. See docs/security-review.md.
        string policy =
            "default-src 'self'; " +
            "base-uri 'self'; " +
            "form-action 'self'; " +
            "frame-ancestors 'none'; " +
            "object-src 'none'; " +
            "img-src 'self' data: blob:; " +
            "font-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval'; " +
            "connect-src 'self' ws: wss:";

        if (https)
        {
            policy += "; upgrade-insecure-requests";
        }

        return policy;
    }
}

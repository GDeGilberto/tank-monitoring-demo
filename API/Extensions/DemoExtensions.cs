using System.Net;
using System.Threading.RateLimiting;
using Infrastructure.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Extensions
{
    /// <summary>Piezas de seguridad del demo publico: solo lectura, limite de peticiones y health checks.</summary>
    public static class DemoExtensions
    {
        public static IServiceCollection AddDemoProtections(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                // El demo corre detras de un tunel o proxy en una red privada de contenedores.
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            int permitLimit = configuration.GetValue("RateLimit:PermitPerMinute", 120);
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/health"))
                        return RateLimitPartition.GetNoLimiter("health");

                    string key = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    });
                });
            });

            return services;
        }

        /// <summary>En modo demo cualquier metodo que no sea de lectura devuelve 403.</summary>
        public static IApplicationBuilder UseDemoReadOnly(this IApplicationBuilder app, IConfiguration configuration)
        {
            if (!configuration.GetValue<bool>("Demo:Enabled")) return app;

            return app.Use(async (context, next) =>
            {
                string method = context.Request.Method;
                if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
                {
                    await next();
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"message\":\"Demo de solo lectura: no se permiten cambios.\"}");
            });
        }

        /// <summary>/health (vivo) y /health/ready (con base de datos).</summary>
        public static IEndpointRouteBuilder MapHealth(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/health", () => Results.Ok(new { status = "ok" }));
            endpoints.MapGet("/health/ready", async (AppDbContext db, CancellationToken ct) =>
                await db.Database.CanConnectAsync(ct)
                    ? Results.Ok(new { status = "ready" })
                    : Results.StatusCode((int)HttpStatusCode.ServiceUnavailable));
            return endpoints;
        }
    }
}

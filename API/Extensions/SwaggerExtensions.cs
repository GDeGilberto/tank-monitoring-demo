using Microsoft.OpenApi.Models;
using System.Reflection;

namespace API.Extensions
{
    /// <summary>Documentacion OpenAPI (solo se activa con Swagger:Enabled=true o en Development).</summary>
    public static class SwaggerExtensions
    {
        public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
        {
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Monitoreo de tanques - API",
                    Version = "v1",
                    Description = "API para consultar sitios, inventarios y descargas de tanques con liquidos. " +
                                  "Autenticacion mediante JWT. Los datos del demo son simulados.",
                });

                options.EnableAnnotations();

                var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
                if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);

                options.CustomSchemaIds(type => type.FullName);

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Encabezado Authorization con el esquema Bearer. Ejemplo: 'Bearer {token}'.",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                        },
                        Array.Empty<string>()
                    },
                });
            });

            return services;
        }

        public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Monitoreo de tanques - API v1");
                c.RoutePrefix = "swagger";
                c.DocumentTitle = "Monitoreo de tanques - API";
            });
            return app;
        }
    }
}

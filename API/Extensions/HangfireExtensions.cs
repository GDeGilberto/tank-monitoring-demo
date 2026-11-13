using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Infrastructure.Jobs;

namespace API.Extensions
{
    public static class HangfireExtensions
    {
        public static IServiceCollection AddHangfireServices(this IServiceCollection services, IConfiguration configuration)
        {
            string connection = configuration.GetConnectionString("HangfireConnection")
                ?? configuration.GetConnectionString("DefaultConnection")!;

            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connection)));

            // Dos trabajadores bastan para el demo y cuidan la memoria en equipos pequenos.
            services.AddHangfireServer(options => options.WorkerCount = configuration.GetValue("Jobs:WorkerCount", 2));

            return services;
        }

        /// <summary>El panel de Hangfire solo se expone si Jobs:Dashboard es true y unicamente desde localhost.</summary>
        public static IApplicationBuilder UseHangfireDashboardWithSecurity(this IApplicationBuilder app, IConfiguration configuration)
        {
            if (configuration.GetValue<bool>("Jobs:Dashboard"))
            {
                app.UseHangfireDashboard("/hangfire", new DashboardOptions
                {
                    Authorization = [new LocalRequestsOnlyAuthorizationFilter()],
                });
            }
            return app;
        }

        /// <summary>Registra los trabajos recurrentes con cron configurables (Jobs:InventoryCron y Jobs:DeliveriesCron).</summary>
        public static void ConfigureRecurringJobs(this IApplicationBuilder app, IConfiguration configuration)
        {
            string inventoryCron = configuration["Jobs:InventoryCron"] ?? "*/3 * * * *";
            string deliveriesCron = configuration["Jobs:DeliveriesCron"] ?? Cron.Daily(6);

            // Se usan los servicios (no las API estaticas) para no depender del panel de Hangfire.
            var recurring = app.ApplicationServices.GetRequiredService<IRecurringJobManager>();
            recurring.AddOrUpdate<InventarioJob>("job-inventario", job => job.Execute(), inventoryCron);
            recurring.AddOrUpdate<DescargasJobs>("job-descargas", job => job.Execute(), deliveriesCron);
        }
    }
}

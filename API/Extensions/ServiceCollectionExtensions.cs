using Application.UseCases;
using Infrastructure.Jobs;

namespace API.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>Analizadores de tramas y trabajos programados.</summary>
        public static IServiceCollection AddJobServices(this IServiceCollection services)
        {
            services.AddScoped<ParceDeliveryReport>();
            services.AddScoped<ParseTankInventoryReport>();
            services.AddScoped<InventarioJob>();
            services.AddScoped<DescargasJobs>();
            return services;
        }
    }
}

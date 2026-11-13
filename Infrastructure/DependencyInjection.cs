using Application.Interfaces;
using Application.Options;
using Application.Services;
using Application.UseCases;
using Domain.Entities;
using Infrastructure.Communication;
using Infrastructure.Data;
using Infrastructure.Dtos;
using Infrastructure.Mappers;
using Infrastructure.Models;
using Infrastructure.Presenters;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Infrastructure.Simulation;
using Infrastructure.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        /// <summary>Base de datos PostgreSQL (cadena "DefaultConnection", normalmente por variable de entorno).</summary>
        public static IServiceCollection AddAppDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
            return services;
        }

        /// <summary>
        /// Lector de tanques segun Reader:Mode: "Serial" (por defecto, puerto real) o "Simulated".
        /// En modo Simulated no se registra SerialPortManager, asi que nunca se toca /dev/tty*.
        /// </summary>
        public static IServiceCollection AddTankReader(this IServiceCollection services, IConfiguration configuration)
        {
            services.TryAddSingleton(TimeProvider.System);

            services.AddOptions<SiteOptions>()
                .BindConfiguration(SiteOptions.SectionName)
                .Validate(o => o.Id > 0, "Sitio:Id debe ser un entero mayor que cero.")
                .ValidateOnStart();

            string mode = configuration["Reader:Mode"] ?? "Serial";
            if (string.Equals(mode, "Simulated", StringComparison.OrdinalIgnoreCase))
            {
                services.AddOptions<SimulationOptions>().BindConfiguration(SimulationOptions.SectionName);
                services.AddSingleton<SimulatedTankReader>();
                services.AddSingleton<ITankReader>(sp => sp.GetRequiredService<SimulatedTankReader>());
            }
            else
            {
                services.AddOptions<SerialPortSettings>()
                    .BindConfiguration("SerialPort")
                    .ValidateOnStart();
                services.AddSingleton<IValidateOptions<SerialPortSettings>, SerialSettingsValidator>();
                services.AddSingleton<SerialPortManager>();
                services.AddSingleton<ISerialPortService>(sp => sp.GetRequiredService<SerialPortManager>());
                services.AddSingleton<ITankReader>(sp => sp.GetRequiredService<SerialPortManager>());
            }

            return services;
        }

        /// <summary>Servicio externo opcional (ExternalService:Enabled). Desactivado: objeto nulo sin red.</summary>
        public static IServiceCollection AddExternalService(this IServiceCollection services, IConfiguration configuration)
        {
            if (configuration.GetValue<bool>("ExternalService:Enabled"))
            {
                services.AddHttpClient<IExternalService, ExternalHttpService>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.Add("User-Agent", "TankMonitoring/1.0");
                });
            }
            else
            {
                services.AddSingleton<IExternalService, NullExternalService>();
            }
            return services;
        }

        /// <summary>Repositorios, mapeadores, presentadores, servicios y casos de uso.</summary>
        public static IServiceCollection AddMonitoringData(this IServiceCollection services)
        {
            services.AddScoped<IRepository<DescargasEntity>, DescargasRepository>();
            services.AddScoped<IRepository<SitiosEntity>, SitiosRepository>();
            services.AddScoped<IRepository<InventarioEntity>, InventarioRepository>();
            services.AddScoped<IRepository<TanqueEntity>, TanqueRepository>();
            services.AddScoped<ITanqueRepository, TanqueRepository>();
            services.AddScoped<IRepositorySearch<ProcDescargaModel, DescargasEntity>, DescargasRepository>();
            services.AddScoped<IRepositorySearch<ProcInventarioModel, InventarioEntity>, InventarioRepository>();

            services.AddScoped<IMapper<DescargaRequestDTO, DescargasEntity>, DescargaMapper>();
            services.AddScoped<IMapper<InventarioRequestDTO, InventarioEntity>, InventarioMapper>();
            services.AddScoped<IPresenter<InventarioEntity, InventarioViewModel>, InventarioPresenter>();

            services.AddScoped<DescargasService<DescargasEntity>>();
            services.AddScoped<InventarioService<InventarioEntity, InventarioViewModel>>();
            services.AddScoped<IExcelExportService, ExcelExportService>();

            services.AddScoped<CreateDescargasUseCase<DescargaRequestDTO>>();
            services.AddScoped<CreateInventarioUseCase<InventarioRequestDTO>>();
            services.AddScoped<GetDescargasUseCase>();
            services.AddScoped<GetDescargaByIdUseCase>();
            services.AddScoped<GetDescargaSearchUseCase<ProcDescargaModel>>();
            services.AddScoped<GetSitiosUseCase>();
            services.AddScoped<GetSitiosByIdUseCase>();
            services.AddScoped<GetInventariosUseCase>();
            services.AddScoped<GetInventarioByIdUseCase>();
            services.AddScoped<GetLatestInventarioByStationUseCase<ProcInventarioModel>>();
            services.AddScoped<GetTanqueBySitioAndNumeroUseCase>();

            return services;
        }
    }
}

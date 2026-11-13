using API.Extensions;
using API.Middleware;
using Hangfire;
using Infrastructure;
using Infrastructure.Data;
using Infrastructure.Jobs;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// Falla al arrancar si faltan secretos o cadenas de conexion (sin imprimir valores).
RequiredConfigValidator.EnsureValid(config);

bool jobsEnabled = config.GetValue("Jobs:Enabled", true);
bool swaggerEnabled = config.GetValue("Swagger:Enabled", builder.Environment.IsDevelopment());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddJwtAuthentication(config);
if (swaggerEnabled) builder.Services.AddSwaggerDocumentation();

builder.Services.AddAppDatabase(config);
builder.Services.AddTankReader(config);
builder.Services.AddExternalService(config);
builder.Services.AddMonitoringData();
builder.Services.AddJobServices();
builder.Services.AddDemoProtections(config);

if (jobsEnabled) builder.Services.AddHangfireServices(config);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseGlobalExceptionHandling();

if (swaggerEnabled) app.UseSwaggerDocumentation();

// Detras de un proxy o tunel (TLS terminado fuera) no se redirige a HTTPS.
if (!config.GetValue<bool>("Demo:Enabled")) app.UseHttpsRedirection();

app.UseDemoReadOnly(config);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealth();

if (config.GetValue<bool>("Database:Initialize"))
{
    await DemoDatabaseInitializer.InitializeAsync(app.Services);
}

if (jobsEnabled)
{
    app.UseHangfireDashboardWithSecurity(config);
    app.ConfigureRecurringJobs(config);

    // Una primera lectura inmediata para que el dashboard tenga datos desde el arranque.
    if (config.GetValue("Jobs:RunOnStart", false))
    {
        var client = app.Services.GetRequiredService<IBackgroundJobClient>();
        client.Enqueue<InventarioJob>(job => job.Execute());
        client.Enqueue<DescargasJobs>(job => job.Execute());
    }
}

app.Logger.LogInformation("API en el entorno {Environment}. Jobs: {Jobs}. Lector: {Reader}",
    app.Environment.EnvironmentName, jobsEnabled, config["Reader:Mode"] ?? "Serial");

app.Run();

/// <summary>Necesario para WebApplicationFactory en las pruebas.</summary>
public partial class Program;

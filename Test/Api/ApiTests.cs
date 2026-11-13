using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Infrastructure.Data;
using Infrastructure.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Test.Api
{
    /// <summary>API en memoria: sin SQL, sin Hangfire, sin puertos. La clave JWT se genera por ejecucion.</summary>
    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public const int SiteId = 7;
        public const string Issuer = "test-issuer";
        public const string Audience = "test-audience";

        public string JwtKey { get; } = Convert.ToBase64String(Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray());
        public bool Demo { get; init; }
        private readonly string _dbName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Jobs:Enabled", "false");
            builder.UseSetting("Reader:Mode", "Simulated");
            builder.UseSetting("Sitio:Id", SiteId.ToString());
            builder.UseSetting("Jwt:Key", JwtKey);
            builder.UseSetting("Jwt:Issuer", Issuer);
            builder.UseSetting("Jwt:Audience", Audience);
            builder.UseSetting("ApiKeys:JobKey", Guid.NewGuid().ToString("N"));
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused;Database=unused");
            builder.UseSetting("Demo:Enabled", Demo ? "true" : "false");
            builder.UseSetting("Swagger:Enabled", "false");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));
                services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
            });
        }

        public void Seed()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (db.CatSitios.Any()) return;
            db.CatSitios.Add(new CatSitiosModel { IdSitio = SiteId, Nombre = "Sitio de prueba" });
            db.CatTanques.Add(new CatTanqueModel { IdSitio = SiteId, NoTanque = "01", Producto = "TipoA", Capacidad = 30000 });
            db.ProcInventarios.Add(new ProcInventarioModel { Idsitio = SiteId, NoTanque = "01", ClaveProducto = "TipoA", VolumenDisponible = 1234, Temperatura = 20, Fecha = new DateTime(2026, 1, 1, 1, 1, 0) });
            db.ProcDescargas.Add(new ProcDescargaModel { IdSitio = SiteId, NoTanque = 1, VolumenInicio = 100, FechaInicio = new DateTime(2026, 1, 1), VolumenDisponible = 900, FechaFinal = new DateTime(2026, 1, 1, 0, 5, 0), CantidadCargada = 800 });
            db.SaveChanges();
        }

        public HttpClient CreateAuthorizedClient()
        {
            var client = CreateClient();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
            var token = new JwtSecurityToken(Issuer, Audience, [new Claim(ClaimTypes.Name, "tester")],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            return client;
        }
    }

    public class ApiTests : IClassFixture<ApiFactory>
    {
        private readonly ApiFactory _factory;

        public ApiTests(ApiFactory factory)
        {
            _factory = factory;
            _factory.Seed();
        }

        [Fact]
        public async Task Inventarios_WithoutToken_Returns401()
        {
            var response = await _factory.CreateClient().GetAsync($"/api/Inventarios/{ApiFactory.SiteId}");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Inventarios_WithToken_ReturnsSeededData()
        {
            var response = await _factory.CreateAuthorizedClient().GetAsync($"/api/Inventarios/{ApiFactory.SiteId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("1234", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Sitios_WithToken_ReturnsTheSite_AndById()
        {
            var client = _factory.CreateAuthorizedClient();
            var all = await client.GetAsync("/api/Sitios");
            Assert.Equal(HttpStatusCode.OK, all.StatusCode);
            Assert.Contains("Sitio de prueba", await all.Content.ReadAsStringAsync());

            var one = await client.GetAsync($"/api/Sitios/{ApiFactory.SiteId}");
            Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        }

        [Fact]
        public async Task Descargas_WithToken_ReturnsSeededData()
        {
            var client = _factory.CreateAuthorizedClient();
            var response = await client.GetAsync("/api/Descargas");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("800", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task InvalidInput_Returns4xx()
        {
            var client = _factory.CreateAuthorizedClient();
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/Inventarios/0")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/Sitios/9999")).StatusCode);
            var post = await client.PostAsync("/api/Inventarios", new StringContent("{\"idSitio\":0,\"noTanque\":0,\"volumenDisponible\":0}", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
        }

        [Fact]
        public async Task Health_IsAnonymous()
        {
            var response = await _factory.CreateClient().GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Swagger_IsOff_ByDefault()
        {
            var response = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public class DemoApiTests : IClassFixture<DemoApiTests.DemoFactory>
    {
        public sealed class DemoFactory : IDisposable
        {
            public ApiFactory Inner { get; } = new() { Demo = true };
            public void Dispose() => Inner.Dispose();
        }

        private readonly ApiFactory _factory;

        public DemoApiTests(DemoFactory factory)
        {
            _factory = factory.Inner;
            _factory.Seed();
        }

        [Fact]
        public async Task Demo_AllowsAnonymousReads()
        {
            var client = _factory.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/Inventarios/{ApiFactory.SiteId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/Sitios")).StatusCode);
        }

        [Theory]
        [InlineData("POST", "/api/Inventarios")]
        [InlineData("POST", "/api/Descargas")]
        [InlineData("POST", "/api/Auth/login")]
        [InlineData("PUT", "/api/Sitios/7")]
        [InlineData("DELETE", "/api/Sitios/7")]
        public async Task Demo_RejectsAnyWrite_With403(string method, string url)
        {
            var request = new HttpRequestMessage(new HttpMethod(method), url)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
            var response = await _factory.CreateClient().SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}

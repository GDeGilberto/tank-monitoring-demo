using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Models;
using Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Test.Support;

namespace Test.Repositories
{
    public class RepositoryReadTests
    {
        private static async Task<ServiceProvider> WithData()
        {
            var sp = TestDb.CreateServices(FakeTankReader.Timeout());
            using var scope = sp.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IRepository<DescargasEntity>>().AddAsync(
                new DescargasEntity(TestDb.SiteId, 1, 100, 20, new DateTime(2026, 1, 1, 1, 0, 0), 900, 21, new DateTime(2026, 1, 1, 1, 5, 0), 800));
            await scope.ServiceProvider.GetRequiredService<IRepository<InventarioEntity>>().AddAsync(
                new InventarioEntity(TestDb.SiteId, 1, "TipoA", 500, 20, new DateTime(2026, 1, 1, 1, 0, 0)));
            return sp;
        }

        [Fact]
        public async Task Descargas_GetAll_GetById_GetAsync()
        {
            using var sp = await WithData();
            using var scope = sp.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRepository<DescargasEntity>>();
            var all = (await repo.GetAllAsync()).ToList();
            Assert.Single(all);
            Assert.Equal(800, (await repo.GetByIdAsync(sp.Get(db => db.ProcDescargas.Single().Id)))!.CantidadCargada);
            await Assert.ThrowsAsync<EntityNotFoundException>(() => repo.GetByIdAsync(999));
            await Assert.ThrowsAsync<ArgumentException>(() => repo.GetByIdAsync(0));

            var search = scope.ServiceProvider.GetRequiredService<IRepositorySearch<ProcDescargaModel, DescargasEntity>>();
            Assert.Single(await search.GetAsync(d => d.NoTanque == 1));
            Assert.Empty(await search.GetAsync(d => d.NoTanque == 4));
        }

        [Fact]
        public async Task Inventario_GetAll_GetById()
        {
            using var sp = await WithData();
            using var scope = sp.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRepository<InventarioEntity>>();
            Assert.Single(await repo.GetAllAsync());
            var id = sp.Get(db => db.ProcInventarios.Single().IdReg);
            Assert.Equal(500, (await repo.GetByIdAsync(id)).VolumenDisponible);
            await Assert.ThrowsAsync<EntityNotFoundException>(() => repo.GetByIdAsync(999));
            await Assert.ThrowsAsync<ArgumentException>(() => repo.GetByIdAsync(-1));
        }

        [Fact]
        public async Task Sitios_And_Tanques_AreReadable()
        {
            using var sp = await WithData();
            using var scope = sp.CreateScope();
            var sites = scope.ServiceProvider.GetRequiredService<IRepository<SitiosEntity>>();
            var all = (await sites.GetAllAsync()).ToList();
            Assert.Equal(4, all.Single().Tanques!.Count);
            Assert.Equal(TestDb.SiteId, (await sites.GetByIdAsync(TestDb.SiteId)).Id);
            await Assert.ThrowsAsync<EntityNotFoundException>(() => sites.GetByIdAsync(12345));

            var tanks = scope.ServiceProvider.GetRequiredService<ITanqueRepository>();
            Assert.Equal("TipoB", (await tanks.GetTanqueBySitioAndNumeroAsync(TestDb.SiteId, 2))!.Producto);
            Assert.Null(await tanks.GetTanqueBySitioAndNumeroAsync(TestDb.SiteId, 9));
        }
    
        [Fact]
        public async Task NullArguments_AreRejected()
        {
            using var sp = await WithData();
            using var scope = sp.CreateScope();
            var d = scope.ServiceProvider.GetRequiredService<IRepository<DescargasEntity>>();
            var i = scope.ServiceProvider.GetRequiredService<IRepository<InventarioEntity>>();
            await Assert.ThrowsAsync<ArgumentNullException>(() => d.AddAsync(null!));
            await Assert.ThrowsAsync<ArgumentNullException>(() => i.AddAsync(null!));
            var ds = scope.ServiceProvider.GetRequiredService<IRepositorySearch<ProcDescargaModel, DescargasEntity>>();
            var isr = scope.ServiceProvider.GetRequiredService<IRepositorySearch<ProcInventarioModel, InventarioEntity>>();
            await Assert.ThrowsAsync<ArgumentNullException>(() => ds.GetAsync(null!));
            await Assert.ThrowsAsync<ArgumentNullException>(() => isr.GetAsync(null!));
        }

        [Fact]
        public async Task Inventario_TankWithoutType_IsRejected()
        {
            using var sp = TestDb.CreateServices(FakeTankReader.Timeout(), seed: false);
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();
                db.CatSitios.Add(new CatSitiosModel { IdSitio = TestDb.SiteId });
                db.CatTanques.Add(new CatTanqueModel { IdSitio = TestDb.SiteId, NoTanque = "01", Producto = null });
                db.SaveChanges();
            }
            using var s2 = sp.CreateScope();
            var repo = s2.ServiceProvider.GetRequiredService<IRepository<InventarioEntity>>();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repo.AddAsync(new InventarioEntity(TestDb.SiteId, 1, "", 1, 1, DateTime.Now)));
        }

        [Fact]
        public async Task Inventario_GetAsync_WithoutMatches_ReturnsEmpty()
        {
            using var sp = await WithData();
            using var scope = sp.CreateScope();
            var isr = scope.ServiceProvider.GetRequiredService<IRepositorySearch<ProcInventarioModel, InventarioEntity>>();
            Assert.Empty(await isr.GetAsync(i => i.Idsitio == 999));
        }
    
        [Fact]
        public async Task DatabaseFailures_AreWrapped_InRepositoryException()
        {
            using var sp = await WithData();
            Infrastructure.Data.AppDbContext Broken()
            {
                using var scope = sp.CreateScope();
                var db = new Infrastructure.Data.AppDbContext(
                    new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Infrastructure.Data.AppDbContext>().UseInMemoryDatabase("x").Options);
                db.Dispose();
                return db;
            }

            var d = new DescargasRepository(Broken());
            await Assert.ThrowsAsync<RepositoryException>(() => d.GetAllAsync());
            await Assert.ThrowsAsync<RepositoryException>(() => d.GetByIdAsync(1));
            await Assert.ThrowsAsync<RepositoryException>(() => d.GetAsync(x => true));
            await Assert.ThrowsAsync<RepositoryException>(() => d.AddAsync(
                new DescargasEntity(1, 1, 1, 1, DateTime.Now, 2, 1, DateTime.Now, 1)));

            var i = new InventarioRepository(Broken());
            await Assert.ThrowsAsync<RepositoryException>(() => i.GetAllAsync());
            await Assert.ThrowsAsync<RepositoryException>(() => i.GetByIdAsync(1));
            await Assert.ThrowsAsync<RepositoryException>(() => i.GetAsync(x => true));

            var e = new SitiosRepository(Broken());
            await Assert.ThrowsAsync<RepositoryException>(() => e.GetAllAsync());
            await Assert.ThrowsAsync<RepositoryException>(() => e.GetByIdAsync(1));
        }
    }
}

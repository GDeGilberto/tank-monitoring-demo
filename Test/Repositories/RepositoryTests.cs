using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Test.Support;

namespace Test.Repositories
{
    public class DescargasRepositoryTests
    {
        private static DescargasEntity Entity(DateTime start, int tank = 1, double v0 = 1000, double v1 = 5000) =>
            new(TestDb.SiteId, tank, v0, 20, start, v1, 21, start.AddMinutes(2), v1 - v0);

        private static async Task Add(ServiceProvider sp, DescargasEntity entity)
        {
            using var scope = sp.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IRepository<DescargasEntity>>().AddAsync(entity);
        }

        [Fact]
        public async Task Add_SavesTheRow()
        {
            using var sp = TestDb.CreateServices(FakeTankReader.Timeout());
            await Add(sp, Entity(new DateTime(2026, 11, 12, 8, 0, 0)));
            Assert.Equal(1, sp.Get(db => db.ProcDescargas.Count()));
        }

        [Fact]
        public async Task ExactDuplicate_IsRejected()
        {
            using var sp = TestDb.CreateServices(FakeTankReader.Timeout());
            var start = new DateTime(2026, 11, 12, 8, 0, 0);
            await Add(sp, Entity(start));
            await Assert.ThrowsAsync<DuplicateEntityException>(() => Add(sp, Entity(start)));
        }

        [Fact]
        public async Task SameDay_DifferentHour_IsAccepted()
        {
            using var sp = TestDb.CreateServices(FakeTankReader.Timeout());
            await Add(sp, Entity(new DateTime(2026, 11, 12, 8, 0, 0)));
            await Add(sp, Entity(new DateTime(2026, 11, 12, 9, 0, 0)));
            Assert.Equal(2, sp.Get(db => db.ProcDescargas.Count()));
        }

        [Fact]
        public async Task SameInstant_DifferentTank_IsAccepted()
        {
            using var sp = TestDb.CreateServices(FakeTankReader.Timeout());
            var start = new DateTime(2026, 11, 12, 8, 0, 0);
            await Add(sp, Entity(start, tank: 1));
            await Add(sp, Entity(start, tank: 2));
            Assert.Equal(2, sp.Get(db => db.ProcDescargas.Count()));
        }
    }

    public class InventarioRepositoryTests
    {
        private static async Task Add(ServiceProvider sp, InventarioEntity entity)
        {
            using var scope = sp.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IRepository<InventarioEntity>>().AddAsync(entity);
        }

        [Fact]
        public async Task Add_Then_DuplicateIsRejected()
        {
            using var sp = TestDb.CreateServices(FakeTankReader.Timeout());
            var entity = new InventarioEntity(TestDb.SiteId, 1, "TipoA", 100, 20, new DateTime(2026, 1, 1, 1, 1, 0));
            await Add(sp, entity);
            await Assert.ThrowsAsync<DuplicateEntityException>(() => Add(sp, entity));
        }

        [Fact]
        public async Task UnknownTank_IsRejected()
        {
            using var sp = TestDb.CreateServices(FakeTankReader.Timeout());
            var entity = new InventarioEntity(TestDb.SiteId, 9, "TipoA", 100, 20, DateTime.Now);
            await Assert.ThrowsAsync<EntityNotFoundException>(() => Add(sp, entity));
        }

        [Fact]
        public async Task Latest_ReturnsTheNewestRowPerTank()
        {
            using var sp = TestDb.CreateServices(FakeTankReader.Timeout());
            await Add(sp, new InventarioEntity(TestDb.SiteId, 1, "TipoA", 100, 20, new DateTime(2026, 1, 1, 1, 1, 0)));
            await Add(sp, new InventarioEntity(TestDb.SiteId, 1, "TipoA", 200, 20, new DateTime(2026, 1, 1, 1, 2, 0)));
            await Add(sp, new InventarioEntity(TestDb.SiteId, 2, "TipoB", 300, 20, new DateTime(2026, 1, 1, 1, 1, 0)));

            using var scope = sp.CreateScope();
            var repo = (IRepositorySearch<Infrastructure.Models.ProcInventarioModel, InventarioEntity>)
                scope.ServiceProvider.GetRequiredService<IRepositorySearch<Infrastructure.Models.ProcInventarioModel, InventarioEntity>>();
            var latest = (await repo.GetAsync(i => i.Idsitio == TestDb.SiteId)).OrderBy(i => i.NoTanque).ToList();

            Assert.Equal(2, latest.Count);
            Assert.Equal(200, latest[0].VolumenDisponible);
        }
    }
}

using Application.UseCases;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Models;
using Infrastructure.ViewModels;
using Microsoft.AspNetCore.Components;
using Web.Services;

namespace Web.Components.Pages
{
    public partial class Dashboard : ComponentBase, IDisposable
    {
        [Inject] private GetSitiosByIdUseCase GetSitiosByIdUseCase { get; set; }
        [Inject] private GetLatestInventarioByStationUseCase<ProcInventarioModel> GetInventarioUseCase { get; set; }
        [Inject] private IConfiguration Configuration { get; set; }
        [Inject] private IInventoryUpdateService InventoryUpdateService { get; set; } = default!;

        private SitiosEntity? SitioEntity;
        private IEnumerable<InventarioEntity>? InventarioEntity;
        private SitioViewModel? SitioViewModel;
        private List<TanqueViewModel>? ListTanques;
        private List<ProductoViewModel>? ListProductos;

        private DateTime dateUpdate { get; set; } = DateTime.Now;

        private bool IsLoadingSitio = true;
        private bool IsLoadingProductos = true;
        private bool IsLoadingChart = true;
        private bool IsLoadingTanques = true;

        protected override async Task OnInitializedAsync()
        {
            // Subscribe to automatic inventory updates
            InventoryUpdateService.OnInventoryUpdated += OnAutomaticInventoryUpdate;
            
            // Start the automatic update service
            await InventoryUpdateService.StartAsync();

            // Los datos se cargan aqui (y no despues del primer render) para que ya vengan en el HTML inicial.
            await LoadInitialData();
        }

        private async Task LoadInitialData()
        {
            int idSitio = Configuration.GetValue<int>("Sitio:Id");
            SitioEntity = await GetSitiosByIdUseCase.ExecuteAsync(idSitio);
            InventarioEntity = await GetInventarioUseCase.ExecuteAsync(s => s.Idsitio == SitioEntity.Id);

            CreateSitioViewModel(SitioEntity);
            IsLoadingSitio = false;
            CreateTanquesViewModel(InventarioEntity);
            IsLoadingTanques = false;
            IsLoadingChart = false;
            CreateProductosViewModel(ListTanques);
            IsLoadingProductos = false;

            dateUpdate = DateTime.Now;
            StateHasChanged();
        }

        private async Task OnAutomaticInventoryUpdate()
        {
            try
            {
                // This method is called every 3 minutes by the service from a background thread
                // We need to use InvokeAsync to marshal to the UI thread
                await InvokeAsync(async () =>
                {
                    try
                    {
                        await UpdateInventory();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error updating inventory in UI thread: {ex.Message}");
                        // Log but don't rethrow to avoid breaking the periodic timer
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during automatic inventory update: {ex.Message}");
            }
        }
        
        public async Task ClickUpdateInventario()
        {
            IsLoadingChart = true;
            IsLoadingProductos = true;
            IsLoadingTanques = true;

            try
            {
                // Demo de solo lectura: los datos los genera el simulador en la API; aqui solo se recargan.
                await UpdateInventory();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating inventory: {ex.Message}");
            }
            finally
            {
                IsLoadingChart = false;
                IsLoadingProductos = false;
                IsLoadingTanques = false;
            }
        }

        public async Task ToggleAutoUpdate()
        {
            try
            {
                if (InventoryUpdateService.IsRunning)
                {
                    await InventoryUpdateService.StopAsync();
                }
                else
                {
                    await InventoryUpdateService.StartAsync();
                }
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error toggling auto-update service: {ex.Message}");
            }
        }

        public async Task UpdateInventory()
        {
            try
            {
                IsLoadingProductos = true;
                IsLoadingTanques = true;

                int idSitio = Configuration.GetValue<int>("Sitio:Id");
                SitioEntity = await GetSitiosByIdUseCase.ExecuteAsync(idSitio);
                InventarioEntity = await GetInventarioUseCase.ExecuteAsync(s => s.Idsitio == SitioEntity.Id);
                dateUpdate = DateTime.Now;

                CreateTanquesViewModel(InventarioEntity);
                IsLoadingTanques = false;
                IsLoadingChart = false;
                CreateProductosViewModel(ListTanques);
                IsLoadingProductos = false;       
            }
            finally
            {
                StateHasChanged();
            }
        }

        private void CreateSitioViewModel(SitiosEntity? sitiosEntity)
        {
            if (sitiosEntity != null)
            {
                SitioViewModel = new SitioViewModel
                {
                    Id = sitiosEntity.Id,
                    Nombre = sitiosEntity.Nombre
                };
            }
        }

        private void CreateTanquesViewModel(IEnumerable<InventarioEntity> inventarioEntity)
        {
            ListTanques = (from info in inventarioEntity
                           join t in SitioEntity.Tanques on info.NoTanque equals int.Parse(t.NoTanque)
                           select new TanqueViewModel
                           {
                               NoTanque = info.NoTanque ?? 0,
                               Producto = Enum.TryParse<TipoProducto>(info.ClaveProducto, out var tipo)
                                ? tipo
                                : TipoProducto.Desconocido,
                               Volumen = (decimal?)info.VolumenDisponible,
                               Capacidad = t.Capacidad,
                               Temperatura = (decimal?)info.Temperatura
                           }).ToList();
        }

        private void CreateProductosViewModel(List<TanqueViewModel>? tanques)
        {
            ListProductos = tanques?
                .GroupBy(t => t.Producto)
                .Select(g => new ProductoViewModel
                {
                    Nombre = g.Key.ToDisplay(),
                    Cantidad = g.Sum(t => t.Volumen ?? 0)
                }).ToList();
        }

        public void Dispose()
        {
            // Unsubscribe from events and stop the service
            if (InventoryUpdateService != null)
            {
                InventoryUpdateService.OnInventoryUpdated -= OnAutomaticInventoryUpdate;
            }
        }
    }
}

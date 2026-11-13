using Infrastructure.ViewModels;
using Microsoft.AspNetCore.Components;

namespace Web.Components.Sitio
{
    public partial class SitioComponent : ComponentBase
    {
        [Parameter]
        public SitioViewModel Sitio { get; set; }

        [Parameter]
        public bool IsLoading { get; set; }

    }
}

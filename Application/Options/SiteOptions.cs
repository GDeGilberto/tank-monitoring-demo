namespace Application.Options
{
    /// <summary>Sitio que monitorea esta instancia (seccion "Sitio").</summary>
    public class SiteOptions
    {
        public const string SectionName = "Sitio";

        /// <summary>Identificador del sitio. Debe ser mayor que cero.</summary>
        public int Id { get; set; }
    }
}

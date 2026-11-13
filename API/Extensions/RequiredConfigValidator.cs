namespace API.Extensions
{
    /// <summary>
    /// Verifica al arrancar que existan los valores obligatorios (secretos y cadenas de conexion).
    /// Los mensajes nombran la variable de entorno que falta y NUNCA imprimen valores.
    /// </summary>
    public static class RequiredConfigValidator
    {
        public const int MinJwtKeyLength = 32;

        public static IReadOnlyList<string> FindProblems(IConfiguration config)
        {
            var problems = new List<string>();
            bool demo = config.GetValue<bool>("Demo:Enabled");
            bool jobsEnabled = config.GetValue("Jobs:Enabled", true);

            // La clave JWT es obligatoria siempre, tambien en Demo.
            string? jwt = config["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwt))
                problems.Add("Falta Jwt__Key (clave de firma JWT, minimo 32 caracteres).");
            else if (jwt.Length < MinJwtKeyLength)
                problems.Add($"Jwt__Key es demasiado corta (minimo {MinJwtKeyLength} caracteres).");

            Require(config, "ConnectionStrings:DefaultConnection", "ConnectionStrings__DefaultConnection", problems);
            if (jobsEnabled)
                Require(config, "ConnectionStrings:HangfireConnection", "ConnectionStrings__HangfireConnection", problems);

            if (!demo)
            {
                Require(config, "ApiKeys:JobKey", "ApiKeys__JobKey", problems);
                if (config.GetValue<bool>("ExternalService:Enabled"))
                    Require(config, "ExternalService:Endpoint", "ExternalService__Endpoint", problems);
            }

            return problems;
        }

        public static void EnsureValid(IConfiguration config)
        {
            var problems = FindProblems(config);
            if (problems.Count > 0)
                throw new InvalidOperationException("Configuracion incompleta: " + string.Join(" ", problems));
        }

        private static void Require(IConfiguration config, string key, string envName, List<string> problems)
        {
            if (string.IsNullOrWhiteSpace(config[key]))
                problems.Add($"Falta {envName}.");
        }
    }
}

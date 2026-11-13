using System.Diagnostics;
using System.Text.RegularExpressions;
using API.Extensions;
using Application.Protocol;
using Microsoft.Extensions.Configuration;

namespace Test.Guards
{
    /// <summary>Guardias de regresion: leen el arbol versionado (git ls-files) y la configuracion obligatoria.</summary>
    public class GuardTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")) && !File.Exists(Path.Combine(dir.FullName, "TankMonitoring.sln")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
        }

        private static IEnumerable<string> TrackedTextFiles()
        {
            string root = RepoRoot();
            string[] extensions = [".cs", ".razor", ".json", ".js", ".md", ".csproj", ".yml", ".yaml", ".css", ".svg", ".sh", ".example", ".txt", ".sln", ".ps1", ".html", ".cshtml"];
            IEnumerable<string> files;
            try
            {
                var psi = new ProcessStartInfo("git", "ls-files --cached --others --exclude-standard") { WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false };
                using var process = Process.Start(psi)!;
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                files = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim());
            }
            catch (Exception)
            {
                files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
                    .Select(f => Path.GetRelativePath(root, f));
            }

            return files
                .Where(f => !f.Replace('\\', '/').Contains("wwwroot/bootstrap/") && !f.Replace('\\', '/').StartsWith("Test/Guards/"))
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()) || Path.GetFileName(f).StartsWith("Dockerfile") || Path.GetFileName(f).StartsWith(".env"))
                .Select(f => Path.Combine(root, f))
                .Where(File.Exists);
        }

        private static readonly string[] ForbiddenTerms =
            [@"D:" + "\\", @"C:\Users", "GDe" + "Gilberto", "Monitoring" + "System"];

        [Fact]
        public void NoLocalPathsOrOriginNames_InTheTree()
        {
            var hits = new List<string>();
            foreach (string file in TrackedTextFiles())
            {
                string text = File.ReadAllText(file);
                foreach (string term in ForbiddenTerms)
                    if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
                        hits.Add($"{Path.GetRelativePath(RepoRoot(), file)}: {term}");
            }
            Assert.True(hits.Count == 0, "Rutas locales o nombres de origen: " + string.Join("; ", hits));
        }

        [Fact]
        public void NoSecretPatterns_InConfigAndSources()
        {
            var patterns = new[]
            {
                new Regex(@"(Host|Server|Data Source)\s*=[^""
]*Password\s*=\s*(?!<|\$|\{)[^;""\s]+", RegexOptions.IgnoreCase), // cadena de conexion con clave literal
                new Regex(@"""(Password|Username|Key|JobKey|Token)""\s*:\s*""(?!<|\$)[^""]+""", RegexOptions.IgnoreCase),
                new Regex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----"),
            };
            var hits = new List<string>();
            foreach (string file in TrackedTextFiles())
            {
                string text = File.ReadAllText(file);
                foreach (var rx in patterns)
                    if (rx.IsMatch(text))
                        hits.Add(Path.GetRelativePath(RepoRoot(), file));
            }
            Assert.True(hits.Count == 0, "Posibles secretos en: " + string.Join("; ", hits.Distinct()));
        }

        [Fact]
        public void OnlyExampleAndDemoAppsettings_AreVersioned()
        {
            string root = RepoRoot();
            foreach (string file in Directory.EnumerateFiles(root, "appsettings*.json", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .Where(f => !f.EndsWith(".Example.json") && !f.EndsWith(".Demo.json")))
            {
                string rel = Path.GetRelativePath(root, file);
                var psi = new ProcessStartInfo("git", $"ls-files --error-unmatch \"{rel}\"") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                using var p = Process.Start(psi)!;
                p.WaitForExit();
                Assert.NotEqual(0, p.ExitCode);
            }
        }

        [Fact]
        public void RequiredConfig_Missing_NamesTheVariable_NeverPrintsValues()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "valor-secreto-que-no-debe-aparecer-en-mensajes-1234567890",
            }).Build();

            var ex = Assert.Throws<InvalidOperationException>(() => RequiredConfigValidator.EnsureValid(config));
            Assert.Contains("ConnectionStrings__DefaultConnection", ex.Message);
            Assert.DoesNotContain("valor-secreto", ex.Message);
        }

        [Fact]
        public void RequiredConfig_JwtKey_IsAlwaysRequired_EvenInDemo()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:Enabled"] = "true",
                ["ConnectionStrings:DefaultConnection"] = "x",
                ["ConnectionStrings:HangfireConnection"] = "x",
            }).Build();

            var problems = RequiredConfigValidator.FindProblems(config);
            Assert.Contains(problems, p => p.Contains("Jwt__Key"));
        }

        [Fact]
        public void RequiredConfig_Demo_DoesNotRequireJobKey_ButProductionDoes()
        {
            var baseConfig = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = new string('k', 40),
                ["ConnectionStrings:DefaultConnection"] = "x",
                ["ConnectionStrings:HangfireConnection"] = "x",
            };

            var demo = new ConfigurationBuilder().AddInMemoryCollection(baseConfig).AddInMemoryCollection(new Dictionary<string, string?> { ["Demo:Enabled"] = "true" }).Build();
            Assert.Empty(RequiredConfigValidator.FindProblems(demo));

            var prod = new ConfigurationBuilder().AddInMemoryCollection(baseConfig).Build();
            Assert.Contains(RequiredConfigValidator.FindProblems(prod), p => p.Contains("ApiKeys__JobKey"));
        }

        [Fact]
        public void ChecksumValidation_CannotBeBypassed()
        {
            // Si alguien quitara la validacion del checksum, esta prueba (y FRM-C) fallarian.
            string frame = "\u0001INV0002611120930&&" + "0000" + "\u0003";
            var ex = Assert.Throws<TankFrameException>(() => TankFrame.Unwrap(frame));
            Assert.Equal(TankFrameError.ChecksumMismatch, ex.Error);
        }
    }
}

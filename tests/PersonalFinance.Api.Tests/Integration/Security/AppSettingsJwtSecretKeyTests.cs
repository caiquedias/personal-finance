using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration.Security
{
    /// <summary>
    /// Teste de regressão de segurança: garante que o appsettings.json de produção
    /// nunca contenha o JwtSettings:SecretKey em texto puro no repositório.
    /// O valor real deve ser injetado via variável de ambiente / secret manager em runtime.
    /// </summary>
    public class AppSettingsJwtSecretKeyTests
    {
        [Fact(DisplayName = "appsettings.json não deve conter JwtSettings:SecretKey hardcoded")]
        public void AppSettingsJson_ShouldNotContainHardcodedSecretKey()
        {
            var appSettingsPath = ResolveAppSettingsPath();

            File.Exists(appSettingsPath).Should().BeTrue(
                $"o arquivo appsettings.json deveria existir em '{appSettingsPath}'");

            // Lê o JSON bruto do disco — não via IConfiguration — para garantir que
            // estamos validando exatamente o que está commitado no repositório.
            var json = File.ReadAllText(appSettingsPath);
            using var document = JsonDocument.Parse(json);

            var secretKey = document.RootElement
                .GetProperty("JwtSettings")
                .GetProperty("SecretKey")
                .GetString();

            secretKey.Should().BeEmpty(
                "o SecretKey do JWT nunca deve ser commitado em texto puro no appsettings.json — " +
                "deve seguir o mesmo padrão de appsettings.Production.json (\"\")");
        }

        /// <summary>
        /// Sobe a partir do diretório de execução dos testes até encontrar a raiz do
        /// repositório (marcada pelo PersonalFinance.sln), evitando depender de
        /// IConfiguration — o teste precisa ler o arquivo-fonte bruto do disco.
        /// </summary>
        private static string ResolveAppSettingsPath()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null &&
                   !File.Exists(Path.Combine(directory.FullName, "PersonalFinance.sln")))
            {
                directory = directory.Parent;
            }

            if (directory is null)
            {
                throw new InvalidOperationException(
                    "Não foi possível localizar a raiz do repositório (PersonalFinance.sln).");
            }

            return Path.Combine(
                directory.FullName, "src", "PersonalFinance.Api", "appsettings.json");
        }
    }
}

using System.Net;
using FluentAssertions;

namespace ScreenSound.Tests.Integracao;

/// <summary>
/// Testes de segurança feitos contra a API REAL rodando (dotnet run no ScreenSound.API).
///
/// Ficam na categoria "Seguranca" (e não "Integracao") porque precisam da API no ar:
/// o CI filtra por Categoria=Integracao, então estes testes não rodam lá.
///
/// Para rodar localmente, com a API no ar:
///   dotnet test --filter Categoria=Seguranca
/// </summary>
[Trait("Categoria", "Seguranca")]
public class SegurancaArtistasTest
{
    private static readonly string UrlApi =
        Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5241";

    [Fact]
    public async Task GetArtistas_DeveRetornar_NaoAutorizado_ParaAcessosAnonimos()
    {
        // Arrange
        using var httpClient = new HttpClient { BaseAddress = new Uri(UrlApi) };

        // Act
        var response = await httpClient.GetAsync("/artistas");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

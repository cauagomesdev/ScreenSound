using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ScreenSound.API.Requests;
using ScreenSound.API.Response;
using ScreenSound.Shared.Modelos.Modelos;

namespace ScreenSound.Tests.Integracao;

/// <summary>
/// Testa os endpoints /generos fazendo requisições HTTP de verdade
/// contra a API rodando em memória.
/// </summary>
[Trait("Categoria", "Integracao")]
public class EndPointGenerosTest : IClassFixture<ScreenSoundApiFactory>
{
    private readonly ScreenSoundApiFactory factory;
    private readonly HttpClient client;

    public EndPointGenerosTest(ScreenSoundApiFactory factory)
    {
        this.factory = factory;
        factory.ResetarBanco();
        client = factory.CriarClienteAutenticado();
    }

    private Genero CadastrarGenero(string nome, string descricao = "Descrição")
    {
        return factory.UsarContexto(contexto =>
        {
            var genero = new Genero { Nome = nome, Descricao = descricao };
            contexto.Generos.Add(genero);
            contexto.SaveChanges();
            return genero;
        });
    }

    // ---------- Autorização ----------

    [Theory]
    [InlineData("GET", "/generos")]
    [InlineData("GET", "/generos/Rock")]
    [InlineData("DELETE", "/generos/1")]
    public async Task Endpoints_SemAutenticacao_DevemRetornar401(string metodo, string url)
    {
        // Arrange
        var clienteAnonimo = factory.CreateClient();

        // Act
        var response = await clienteAnonimo.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), url));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- POST /generos ----------

    [Fact]
    public async Task PostGenero_DeveSalvarGenero()
    {
        // Act
        var response = await client.PostAsJsonAsync("/generos", new GeneroRequest("Rock", "Guitarras distorcidas"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var salvo = factory.UsarContexto(c => c.Generos.Single());
        salvo.Nome.Should().Be("Rock");
        salvo.Descricao.Should().Be("Guitarras distorcidas");
    }

    // ---------- GET /generos ----------

    [Fact]
    public async Task GetGeneros_DeveRetornarTodosOsGeneros()
    {
        // Arrange
        var rock = CadastrarGenero("Rock", "Guitarras");
        var mpb = CadastrarGenero("MPB", "Música popular brasileira");

        // Act
        var generos = await client.GetFromJsonAsync<List<GeneroResponse>>("/generos");

        // Assert
        generos.Should().BeEquivalentTo(new[]
        {
            new GeneroResponse(rock.Id, "Rock", "Guitarras"),
            new GeneroResponse(mpb.Id, "MPB", "Música popular brasileira"),
        });
    }

    // ---------- GET /generos/{nome} ----------

    [Fact]
    public async Task GetGeneroPorNome_Existente_DeveIgnorarMaiusculasEMinusculas()
    {
        // Arrange
        var rock = CadastrarGenero("Rock", "Guitarras");

        // Act
        var response = await client.GetAsync("/generos/rOcK");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var genero = await response.Content.ReadFromJsonAsync<GeneroResponse>();
        genero.Should().Be(new GeneroResponse(rock.Id, "Rock", "Guitarras"));
    }

    [Fact]
    public async Task GetGeneroPorNome_Inexistente_DeveRetornar404()
    {
        // Act
        var response = await client.GetAsync("/generos/NaoExiste");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- DELETE /generos/{id} ----------

    [Fact]
    public async Task DeleteGenero_Existente_DeveRemoverERetornar204()
    {
        // Arrange
        var rock = CadastrarGenero("Rock");
        CadastrarGenero("MPB");

        // Act
        var response = await client.DeleteAsync($"/generos/{rock.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        factory.UsarContexto(c => c.Generos.Select(g => g.Nome).ToList())
            .Should().Equal("MPB");
    }

    [Fact]
    public async Task DeleteGenero_Inexistente_DeveRetornar404()
    {
        // Act
        var response = await client.DeleteAsync("/generos/999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

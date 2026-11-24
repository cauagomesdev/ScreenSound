using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ScreenSound.API.Requests;
using ScreenSound.API.Response;
using ScreenSound.Modelos;
using ScreenSound.Shared.Modelos.Modelos;

namespace ScreenSound.Tests.Integracao;

/// <summary>
/// Testa os endpoints /musicas fazendo requisições HTTP de verdade
/// contra a API rodando em memória.
/// </summary>
[Trait("Categoria", "Integracao")]
public class EndPointMusicasTest : IClassFixture<ScreenSoundApiFactory>
{
    private readonly ScreenSoundApiFactory factory;
    private readonly HttpClient client;

    public EndPointMusicasTest(ScreenSoundApiFactory factory)
    {
        this.factory = factory;
        factory.ResetarBanco();
        client = factory.CriarClienteAutenticado();
    }

    private Artista CadastrarArtista(string nome)
    {
        return factory.UsarContexto(contexto =>
        {
            var artista = new Artista(nome, "Bio");
            contexto.Artistas.Add(artista);
            contexto.SaveChanges();
            return artista;
        });
    }

    private Musica CadastrarMusica(string nome, int artistaId, int? ano = 2000)
    {
        return factory.UsarContexto(contexto =>
        {
            var musica = new Musica(nome) { ArtistaId = artistaId, AnoLancamento = ano };
            contexto.Musicas.Add(musica);
            contexto.SaveChanges();
            return musica;
        });
    }

    private void CadastrarGenero(string nome)
    {
        factory.UsarContexto(contexto =>
        {
            contexto.Generos.Add(new Genero { Nome = nome, Descricao = $"Descrição de {nome}" });
            return contexto.SaveChanges();
        });
    }

    // ---------- Autorização ----------

    [Theory]
    [InlineData("GET", "/musicas")]
    [InlineData("GET", "/musicas/Oceano")]
    [InlineData("DELETE", "/musicas/1")]
    public async Task Endpoints_SemAutenticacao_DevemRetornar401(string metodo, string url)
    {
        // Arrange
        var clienteAnonimo = factory.CreateClient();

        // Act
        var response = await clienteAnonimo.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), url));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- GET /musicas ----------

    [Fact]
    public async Task GetMusicas_SemDados_DeveRetornarListaVazia()
    {
        // Act
        var response = await client.GetAsync("/musicas");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var musicas = await response.Content.ReadFromJsonAsync<List<MusicaResponse>>();
        musicas.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetMusicas_ComDados_DeveRetornarMusicasComNomeDoArtista()
    {
        // Arrange
        var djavan = CadastrarArtista("Djavan");
        var gal = CadastrarArtista("Gal Costa");
        var oceano = CadastrarMusica("Oceano", djavan.Id);
        var baby = CadastrarMusica("Baby", gal.Id);

        // Act
        var musicas = await client.GetFromJsonAsync<List<MusicaResponse>>("/musicas");

        // Assert
        musicas.Should().BeEquivalentTo(new[]
        {
            new MusicaResponse(oceano.Id, "Oceano", djavan.Id, "Djavan"),
            new MusicaResponse(baby.Id, "Baby", gal.Id, "Gal Costa"),
        });
    }

    // ---------- GET /musicas/{nome} ----------

    [Fact]
    public async Task GetMusicaPorNome_Existente_DeveIgnorarMaiusculasEMinusculas()
    {
        // Arrange
        var djavan = CadastrarArtista("Djavan");
        var oceano = CadastrarMusica("Oceano", djavan.Id);

        // Act
        var response = await client.GetAsync("/musicas/oCeAnO");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var musica = await response.Content.ReadFromJsonAsync<MusicaResponse>();
        musica.Should().Be(new MusicaResponse(oceano.Id, "Oceano", djavan.Id, "Djavan"));
    }

    [Fact]
    public async Task GetMusicaPorNome_Inexistente_DeveRetornar404()
    {
        // Act
        var response = await client.GetAsync("/musicas/NaoExiste");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- POST /musicas ----------

    [Fact]
    public async Task PostMusica_SemGeneros_DeveSalvarMusica()
    {
        // Arrange
        var djavan = CadastrarArtista("Djavan");
        var request = new MusicaRequest("Oceano", djavan.Id, 1989);

        // Act
        var response = await client.PostAsJsonAsync("/musicas", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.UsarContexto(c =>
        {
            var salva = c.Musicas.Single();
            salva.Nome.Should().Be("Oceano");
            salva.ArtistaId.Should().Be(djavan.Id);
            salva.AnoLancamento.Should().Be(1989);
            salva.Generos.Should().BeEmpty();
            return salva;
        });
    }

    [Fact]
    public async Task PostMusica_ComGeneros_DeveReaproveitarGeneroExistenteECriarONovo()
    {
        // Arrange
        var djavan = CadastrarArtista("Djavan");
        CadastrarGenero("MPB");
        var generos = new List<GeneroRequest>
        {
            new("mpb", "Já existe, deve ser reaproveitado"),
            new("Jazz", "Gênero novo"),
        };
        var request = new MusicaRequest("Oceano", djavan.Id, 1989, generos);

        // Act
        var response = await client.PostAsJsonAsync("/musicas", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.UsarContexto(c =>
        {
            c.Musicas.Single().Generos.Select(g => g.Nome).Should().BeEquivalentTo(["MPB", "Jazz"]);
            c.Generos.Select(g => g.Nome).ToList().Should().BeEquivalentTo(["MPB", "Jazz"]);
            return 0;
        });
    }

    // ---------- PUT /musicas ----------

    [Fact]
    public async Task PutMusica_Existente_DeveAtualizarNomeEAno()
    {
        // Arrange
        var djavan = CadastrarArtista("Djavan");
        var musica = CadastrarMusica("Oceano", djavan.Id, 1980);
        var request = new MusicaRequestEdit(musica.Id, "Oceano (ao vivo)", djavan.Id, 1999);

        // Act
        var response = await client.PutAsJsonAsync("/musicas", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var atualizada = factory.UsarContexto(c => c.Musicas.Single(m => m.Id == musica.Id));
        atualizada.Nome.Should().Be("Oceano (ao vivo)");
        atualizada.AnoLancamento.Should().Be(1999);
    }

    [Fact]
    public async Task PutMusica_Inexistente_DeveRetornar404()
    {
        // Arrange
        var request = new MusicaRequestEdit(999, "Nome", 1, 2000);

        // Act
        var response = await client.PutAsJsonAsync("/musicas", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- DELETE /musicas/{id} ----------

    [Fact]
    public async Task DeleteMusica_Existente_DeveRemoverERetornar204()
    {
        // Arrange
        var djavan = CadastrarArtista("Djavan");
        var oceano = CadastrarMusica("Oceano", djavan.Id);
        CadastrarMusica("Flor de Lis", djavan.Id);

        // Act
        var response = await client.DeleteAsync($"/musicas/{oceano.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        factory.UsarContexto(c => c.Musicas.Select(m => m.Nome).ToList())
            .Should().Equal("Flor de Lis");
    }

    [Fact]
    public async Task DeleteMusica_Inexistente_DeveRetornar404()
    {
        // Act
        var response = await client.DeleteAsync("/musicas/999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

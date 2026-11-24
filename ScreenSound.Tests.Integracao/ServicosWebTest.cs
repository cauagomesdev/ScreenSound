using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using ScreenSound.Modelos;
using ScreenSound.Web.Requests;
using ScreenSound.Web.Services;

namespace ScreenSound.Tests.Integracao;

/// <summary>
/// Testa os serviços do front (ScreenSound.Web) conversando com a API de verdade,
/// que roda em memória. Assim o caminho Blazor -> HTTP -> API -> banco é exercitado inteiro.
/// </summary>
[Trait("Categoria", "Integracao")]
public class ServicosWebTest : IClassFixture<ScreenSoundApiFactory>
{
    private readonly ScreenSoundApiFactory factory;
    private readonly int pessoaId;
    private readonly ArtistaAPI artistaApi;
    private readonly MusicaAPI musicaApi;

    public ServicosWebTest(ScreenSoundApiFactory factory)
    {
        this.factory = factory;
        pessoaId = factory.ResetarBanco();

        // Os serviços pedem o HttpClient "API" à fábrica; entregamos o cliente da API em memória.
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient("API"))
            .Returns(() => factory.CriarClienteAutenticado());

        artistaApi = new ArtistaAPI(httpClientFactory.Object);
        musicaApi = new MusicaAPI(httpClientFactory.Object);
    }

    private Artista CadastrarArtista(string nome, string bio = "Bio")
    {
        return factory.UsarContexto(contexto =>
        {
            var artista = new Artista(nome, bio);
            contexto.Artistas.Add(artista);
            contexto.SaveChanges();
            return artista;
        });
    }

    // ---------- ArtistaAPI ----------

    [Fact]
    public async Task AddArtistaAsync_DeveCadastrarArtistaNaAPI()
    {
        // Arrange
        var foto = Convert.ToBase64String([1, 2, 3]);

        // Act
        await artistaApi.AddArtistaAsync(new ArtistaRequest("Djavan", "Cantor alagoano", foto));

        // Assert
        var salvo = factory.UsarContexto(c => c.Artistas.Single());
        salvo.Nome.Should().Be("Djavan");
        salvo.Bio.Should().Be("Cantor alagoano");
    }

    [Fact]
    public async Task GetArtistasAsync_DeveRetornarArtistasCadastrados()
    {
        // Arrange
        CadastrarArtista("Djavan");
        CadastrarArtista("Gal Costa");

        // Act
        var artistas = await artistaApi.GetArtistasAsync();

        // Assert
        artistas!.Select(a => a.Nome).Should().BeEquivalentTo(["Djavan", "Gal Costa"]);
    }

    [Fact]
    public async Task GetArtistaPorNomeAsync_Existente_DeveRetornarArtista()
    {
        // Arrange
        var djavan = CadastrarArtista("Djavan", "Cantor alagoano");

        // Act
        var artista = await artistaApi.GetArtistaPorNomeAsync("Djavan");

        // Assert
        artista!.Id.Should().Be(djavan.Id);
        artista.Bio.Should().Be("Cantor alagoano");
    }

    [Fact]
    public async Task GetArtistaPorNomeAsync_Inexistente_DeveLancarExcecao()
    {
        // Act
        var acao = () => artistaApi.GetArtistaPorNomeAsync("NaoExiste");

        // Assert
        await acao.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task UpdateArtistaAsync_DeveAtualizarArtistaNaAPI()
    {
        // Arrange
        var artista = CadastrarArtista("Djavan", "Bio antiga");

        // Act
        await artistaApi.UpdateArtistaAsync(new ArtistaRequestEdit(artista.Id, "Djavan Caetano", "Bio nova", null));

        // Assert
        var atualizado = factory.UsarContexto(c => c.Artistas.Single());
        atualizado.Nome.Should().Be("Djavan Caetano");
        atualizado.Bio.Should().Be("Bio nova");
    }

    [Fact]
    public async Task DeleteArtistaAsync_DeveRemoverArtistaNaAPI()
    {
        // Arrange
        var artista = CadastrarArtista("Djavan");

        // Act
        await artistaApi.DeleteArtistaAsync(artista.Id);

        // Assert
        factory.UsarContexto(c => c.Artistas.Count()).Should().Be(0);
    }

    [Fact]
    public async Task AvaliaArtistaAsync_DeveSalvarNotaEGetAvaliacaoDeveDevolverLa()
    {
        // Arrange
        var artista = CadastrarArtista("Djavan");

        // Act
        await artistaApi.AvaliaArtistaAsync(artista.Id, 4);
        var avaliacao = await artistaApi.GetAvaliacaoDaPessoaLogadaAsync(artista.Id);

        // Assert
        factory.UsarContexto(c => c.AvaliacaoArtistas.AsNoTracking().Single())
            .Should().BeEquivalentTo(new { ArtistaId = artista.Id, PessoaId = pessoaId, Nota = 4 });
        avaliacao!.ArtistaId.Should().Be(artista.Id);
        avaliacao.Nota.Should().Be(4);
    }

    // ---------- MusicaAPI ----------

    [Fact]
    public async Task GetMusicasAsync_DeveRetornarMusicasComNomeDoArtista()
    {
        // Arrange
        var djavan = CadastrarArtista("Djavan");
        factory.UsarContexto(c =>
        {
            c.Musicas.Add(new Musica("Oceano") { ArtistaId = djavan.Id, AnoLancamento = 1989 });
            return c.SaveChanges();
        });

        // Act
        var musicas = await musicaApi.GetMusicasAsync();

        // Assert
        var musica = musicas.Should().ContainSingle().Subject;
        musica.Nome.Should().Be("Oceano");
        musica.ArtistaId.Should().Be(djavan.Id);
        musica.NomeArtista.Should().Be("Djavan");
    }
}

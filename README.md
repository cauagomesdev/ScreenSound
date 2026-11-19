# ScreenSound

[![Executar Testes](https://github.com/cauagomesdev/ScreenSound/actions/workflows/tests.yml/badge.svg)](https://github.com/cauagomesdev/ScreenSound/actions/workflows/tests.yml)

Aplicação de catálogo de artistas e músicas construída nos cursos de .NET da Alura e evoluída depois deles.

## Projetos

| Projeto | Descrição |
|---|---|
| `ScreenSound.API` | API (minimal APIs) com Identity, autenticação por cookies e avaliação de artistas |
| `ScreenSound.Web` | Front-end em Blazor WebAssembly com MudBlazor |
| `ScreenSound.Shared.Dados` | Entity Framework Core, contexto e migrations (SQL Server) |
| `ScreenSound.Shared.Modelos` | Modelos de domínio |
| `ScreenSound` | Aplicação de console |
| `ScreenSound.Tests.Unitarios` | Testes unitários (xUnit, banco em memória) |
| `ScreenSound.Tests.Integracao` | Testes de integração da API (WebApplicationFactory, banco em memória) |

## Como rodar

Pré-requisitos: SDK do .NET 10 e Docker.

```bash
docker run -d --name sqlserver -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=ScreenSound@123" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
```

```bash
cd ScreenSound.API
dotnet tool restore
dotnet ef database update --project ../ScreenSound.Shared.Dados
dotnet run --launch-profile https
```

Em outro terminal:

```bash
dotnet run --project ScreenSound.Web --launch-profile https
```

- Site: https://localhost:7015
- Swagger da API: https://localhost:7089/swagger

## Testes

```bash
dotnet test ScreenSound.sln
```

Os testes usam banco em memória, então não precisam do SQL Server. Eles rodam automaticamente no GitHub Actions a cada push e pull request no `main`.

# CrowdDiscussesAlternatives
## What is it?

A platform for proposal discussion and voting

Rather than one person writing a whole solution and everyone else agreeing or disagreeing,
a solution here is **assembled from many sentence-sized proposals** drawn from a shared pool.
Different groupings of those proposals become competing alternative solutions, which can then
be discussed, evaluated against agreed requirements, and ranked.

See [Documentation/devplan.md](Documentation/devplan.md) for the data model, the design
decisions and the delivery plan.

## Getting started

Requirements: the **.NET 10 SDK** and access to a MariaDB database. No Docker needed.

```bash
git clone https://github.com/vaspervnp/CrowdDiscussesAlternatives.git
cd CrowdDiscussesAlternatives/app
```

The connection string is **not** in the repository and never should be — this repository is
public. Supply it through user secrets:

```bash
dotnet user-secrets set "ConnectionStrings:Cda" \
  "Server=<host>;Port=3306;Database=CrowdDiscussesAlternatives;User ID=<user>;Password=<password>;SslMode=VerifyFull;" \
  --project src/CDA.Web
```

`SslMode=VerifyFull` is required for any database reached over a network; the application
refuses to start otherwise. Loopback addresses are exempt, since that traffic never leaves the
machine. Alternatively supply the environment variable `ConnectionStrings__Cda`.

Then bring the schema up to date and run:

```bash
dotnet ef database update --project src/CDA.Infrastructure --startup-project src/CDA.Web
dotnet run --project src/CDA.Web
```

`GET /health` reports whether the database is reachable. In development, the OpenAPI document
is served at `/openapi/v1.json`.

## Tests

```bash
dotnet test tests/CDA.UnitTests            # no database required
dotnet test tests/CDA.IntegrationTests     # needs a disposable database
```

**CI runs the unit tests only.** The integration tests need a database and are not run on
GitHub, so please run them locally before merging — they are the only coverage the
database-dependent code has.

Integration tests truncate every table between test classes, so they need their own database
and refuse to run against anything whose name does not end in `_Test`:

```bash
dotnet user-secrets set "ConnectionStrings:CdaTest" \
  "Server=<host>;Port=3306;Database=CrowdDiscussesAlternatives_Test;User ID=<user>;Password=<password>;SslMode=VerifyFull;" \
  --project tests/CDA.IntegrationTests
```

## Layout

| Project | Contains |
|---|---|
| `src/CDA.Domain` | Entities and invariants. References nothing. |
| `src/CDA.Application` | Use cases, DTOs, abstractions. |
| `src/CDA.Infrastructure` | EF Core context, migrations, external services. |
| `src/CDA.Web` | MVC views and REST API in one host. |

## Adding a migration

```bash
dotnet ef migrations add <Name> \
  --project src/CDA.Infrastructure --startup-project src/CDA.Web \
  --output-dir Persistence/Migrations
```

## License

See [LICENSE](LICENSE).

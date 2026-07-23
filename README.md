# CrowdDiscussesAlternatives
## What is it?

A platform for proposal discussion and voting

Rather than one person writing a whole solution and everyone else agreeing or disagreeing,
a solution here is **assembled from many sentence-sized proposals** drawn from a shared pool.
Different groupings of those proposals become competing alternative solutions, which can then
be discussed, evaluated against agreed requirements, and ranked.

- [Documentation/manual.md](Documentation/manual.md) — how to use the platform, with
  screenshots, and an honest list of what is not built yet.
- [Documentation/devplan.md](Documentation/devplan.md) — the data model, the design decisions
  and the delivery plan.

## Getting started

Requirements: the **.NET 10 SDK** and access to a MariaDB database. No Docker needed.

```bash
git clone https://github.com/vaspervnp/CrowdDiscussesAlternatives.git
cd CrowdDiscussesAlternatives/app
```

The connection string is **not** in the repository and never should be: git history outlives
any later deletion, and this project is meant to be open-sourced. Supply it through user
secrets:

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

## Configuring a self-contained deployment

On a target machine you would publish the app **self-contained**, so there is no .NET SDK there
and `dotnet user-secrets` is unavailable. The **`CDA.Configure`** console tool does the same job
without it: it writes `ConnectionStrings:Cda` into the very user-secrets file the app reads
(`%APPDATA%\Microsoft\UserSecrets\cda-web-secrets\secrets.json` on Windows,
`~/.microsoft/usersecrets/cda-web-secrets/secrets.json` elsewhere), so the credential stays out
of both the deployment folder and the repository. The web app loads that store in every
environment, not just development.

Publish it alongside the app, then run it on the target:

```bash
dotnet publish src/CDA.Configure -c Release -r <rid> --self-contained

cda-configure                                              # interactive wizard
cda-configure --host <host> --user <user> --password <secret>
cda-configure --show      # print the saved value, password masked
cda-configure --path      # print the path to the secrets file
```

It defaults `Port=3306`, `Database=CrowdDiscussesAlternatives` and `SslMode=VerifyFull`, refuses
the same unverified-transport strings the app refuses, and **opens a real connection to check the
credentials before saving** (`--no-test` skips that). User secrets live in the running user's
profile, so run it as the **same account** the app runs as — it prints the exact file it wrote.

With the connection string in place, bring the schema up to date with the **`CDA.Migrate`** tool,
which applies the EF Core migrations without `dotnet ef` (they are compiled into the app):

```bash
dotnet publish src/CDA.Migrate -c Release -r <rid> --self-contained

cda-migrate            # apply every pending migration
cda-migrate status     # list what is applied and what is pending
```

It reads the connection string the same way the app does (`--connection` flag, then
`ConnectionStrings__Cda`, then the saved user secret). So a first-time deployment is:

```
cda-configure  →  cda-migrate  →  start the app
```

and after deploying a build that adds migrations, run `cda-migrate` again before restarting.

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
| `src/CDA.Configure` | Console tool to set the DB connection string on a machine without the SDK. |
| `src/CDA.Migrate` | Console tool to apply the EF Core migrations without `dotnet ef`. |

## Adding a migration

```bash
dotnet ef migrations add <Name> \
  --project src/CDA.Infrastructure --startup-project src/CDA.Web \
  --output-dir Persistence/Migrations
```

## License

See [LICENSE](LICENSE).

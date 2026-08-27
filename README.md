# BaseRepository

[![CI](https://github.com/alireza-flhz/CleanArchitecture-Api-Template/actions/workflows/ci.yml/badge.svg)](https://github.com/alireza-flhz/CleanArchitecture-Api-Template/actions/workflows/ci.yml)

A Clean Architecture starter template for .NET 10 APIs. Everything most
projects need on day one — layering, generic CRUD, authentication, paging,
validation, OpenAPI docs, structured logging — already wired up and working.

📄 **[نسخهٔ فارسی این راهنما](https://github.com/alireza-flhz/CleanArchitecture-Api-Template/blob/master/README.fa.md)**

---

## Quick start

**Supports .NET 10 and .NET 9.** It targets .NET 10 by default; on the .NET 9
SDK pass `--framework net9.0` and everything — including the full test suite — works the
same. Check what you have with `dotnet --version`.

Install the template from NuGet, scaffold a project under your own name, and
run it:

```bash
dotnet new install BaseRepository.Template::1.0.0-preview.1
dotnet new basecrud -n Acme.Store
cd Acme.Store
dotnet run --project src/Api
```

[![NuGet](https://img.shields.io/nuget/vpre/BaseRepository.Template.svg)](https://www.nuget.org/packages/BaseRepository.Template)

The package is currently a prerelease, so the version is pinned explicitly above
— `dotnet new install BaseRepository.Template` without a version only resolves
stable releases. You can also install from a local clone or a `.nupkg` you built
yourself:

```bash
dotnet new install <path-to-this-repo-or-its-.nupkg>
```

Everything — namespaces, project files, the solution — is renamed to the name
you pass to `-n`. No find-and-replace needed.

Any name works. One that is not a valid C# identifier is normalised the same
way everywhere, so `-n my-api` gives you a `my-api` folder containing
`my_api.sln` and `my_api.*.csproj` with matching namespaces — consistent, and it
builds.

### Options

| Option | Values | Default | Meaning |
| --- | --- | --- | --- |
| `--framework` | `net10.0`, `net9.0` | `net10.0` | Target framework. Pick what your SDK can build. |
| `--skipRestore` | flag | off | Skip the automatic `dotnet restore` after creation. |

`dotnet new basecrud --help` lists these at any time, and scaffolding prints the
next steps (signing key, run, test, add your own entity).

You can also just clone this repo and run it directly:

```bash
dotnet run --project src/Api
```

### What you get immediately

| Endpoint | Description |
| --- | --- |
| `GET /health` | Health check |
| `GET /` | Service status |
| `GET /openapi/v1.json` | OpenAPI document |
| `GET /scalar/v1` | Interactive API docs (Scalar) |

These work with zero configuration. A SQLite database (`app.db`) is created
automatically on first run.

---

## Configuration

Only one setting is required before you can use protected endpoints: the JWT
signing key. It is intentionally left blank in `appsettings.json` — never ship
a default secret.

```bash
dotnet user-secrets init --project src/Api
dotnet user-secrets set Jwt:SigningKey "<at least 32 bytes>" --project src/Api
```

| Setting | Purpose |
| --- | --- |
| `Jwt:SigningKey` | Key used to sign and validate tokens (**required**) |
| `Jwt:Issuer` | Token issuer |
| `Jwt:Audience` | Token audience |
| `ConnectionStrings:Default` | Database connection string |

Environment variables work too, if you prefer them over user-secrets.

---

## Authentication

Register and login are part of the template — not a sample to delete.

**Register** — `POST /api/v1/auth/register` → **201 Created**

```bash
curl -X POST http://localhost:5000/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"me@example.com","password":"correct-horse-battery"}'
# => { "userId": 1, "email": "me@example.com", "token": "...", "expiresAt": "..." }
```

**Login** — `POST /api/v1/auth/login` → **200 OK** (same body, same response shape).

Then send the token on any protected request:

```bash
curl http://localhost:5000/api/v1/todo-items \
  -H "Authorization: Bearer <token>"
```

Details worth knowing:

- Passwords are hashed with BCrypt — never stored or compared in plain text.
- **Email is the only login identifier.** A phone number is never a substitute.
- Registering with an email already taken returns **409**.
- A wrong password and an unknown email both return the same **401**, so the
  response never reveals whether an email is registered.

### Managing your phone number

`PUT /api/v1/auth/me/phone` — requires a bearer token. The user being edited
comes from the token, so nobody can edit someone else's profile.

```bash
curl -X PUT http://localhost:5000/api/v1/auth/me/phone \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber":"09123456789","region":"IR"}'
# => { "userId": 1, "email": "me@example.com", "phoneNumber": "+989123456789" }
```

Numbers from **any country** are supported and stored in E.164 form
(`+989123456789`, `+16502530000`, …). Either pass a number already in
international form and omit `region`, or pass a local-format number with its
ISO 3166-1 alpha-2 `region` (`"IR"`, `"US"`, `"GB"`, …). A local-format number
with no region is rejected as ambiguous rather than guessed at. Send `null` to
clear the number; a number already used by another account returns **409**.

---

## Adding your own entity

This is the main thing the template is for. Three small steps:

**1. The entity** (Domain):

```csharp
public class Product : BaseEntity<int>, IAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    // IAuditableEntity gives you automatic CreatedAt / LastModifiedAt
}
```

**2. A DTO + registration** (Application). Implement `IHasId<TKey>` on the DTO
if you want `POST` responses to carry a proper `Location` header:

```csharp
public record ProductDto(int Id, string Name, decimal Price) : IHasId<int>;

// in your DI setup:
services.AddCrudHandlers<Product, int, ProductDto>();
```

**3. A controller** (Api):

```csharp
[Route("api/v{version:apiVersion}/products")]
public class ProductsController : BaseCrudController<Product, int, ProductDto>
{
    public ProductsController(ISender sender) : base(sender) { }
}
```

That's it — you now have full REST CRUD:

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/api/v1/products?pageIndex=1&pageSize=20` | Paged list |
| `GET` | `/api/v1/products/{id}` | Single item |
| `POST` | `/api/v1/products` | Create |
| `PUT` | `/api/v1/products/{id}` | Update |
| `DELETE` | `/api/v1/products/{id}` | Delete |

All of it requires a bearer token by default. Add `[AllowAnonymous]` on your
controller to opt out.

### Validation (optional)

Drop an `AbstractValidator<T>` anywhere in the Application project — it is
discovered and registered automatically:

```csharp
public class CreateProductValidator : AbstractValidator<CreateCommand<Product, int, ProductDto>>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Dto.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dto.Price).GreaterThan(0);
    }
}
```

An entity with no validator simply skips validation. Failures come back as
**400** with a per-property `errors` object.

### Custom queries

For anything beyond CRUD, derive a `Specification<T>` to describe a
filter/include/order/paging query without touching EF Core, then pass it to
`IRepository<TEntity, TKey>`. For real business rules, write a bespoke
`IRequest`/`IRequestHandler` pair — the generic path is the default, not a
mandate.

---

## The sample entity

`TodoItem` (entity, DTO, validator, controller, `DbSet`) is a real, working
example wired into the running app so you can see the whole pattern end to
end. It is the one thing here meant to be deleted — once your own entities are
in, remove it. Every one of its types carries a doc comment saying so.

---

## Features

**Architecture**

- Four layers: `Domain` → `Application` → `Infrastructure` → `Api`, with
  dependencies pointing only inward. `Domain` references nothing.
- `Result` / `Result<T>` for uniform success/failure, `PagedResult<T>` for page
  metadata (`TotalPages`, `HasNextPage`, `HasPreviousPage`).
- Domain exceptions (`NotFoundException`, `ConflictException`,
  `BusinessRuleException`, `AuthenticationFailedException`) that the API layer
  translates into HTTP status codes automatically.

**Data access**

- Generic `IRepository<TEntity, TKey>` with an EF Core implementation that
  works with any `DbContext`, plus `IUnitOfWork` for explicit saves and
  `IUnitOfWork.BeginTransactionAsync()` when several writes — or a write and
  the work after it — have to succeed or fail together. Commit explicitly;
  disposing an uncommitted transaction rolls it back, so `await using` is the
  safe default.
- Specification pattern for composing queries without leaking EF Core into
  Application code.
- Opt-in `IAuditableEntity` (automatic timestamps) and `ISoftDelete` (deletes
  become updates, and soft-deleted rows are filtered out globally).

**API**

- Global exception handling: every error comes back as RFC 7807
  `ProblemDetails` — 404, 409, 401, 422, 400, 500 mapped from domain
  exceptions.
- API versioning (`api/v{version:apiVersion}/…`) with one OpenAPI document per
  version.
- Output caching is wired up but deliberately **not** applied to CRUD
  controllers — caching an authorized response without varying by user leaks
  one user's data to another. Apply `[OutputCache]` yourself where you have
  thought it through.
- Structured logging via Serilog — one summary line per request.

**Licensing**

Every dependency is permissively licensed (MIT / Apache-2.0). MediatR and
AutoMapper are deliberately **not** used — their v13+ license could force a
closed-source project to open its source or buy a license. In their place: a
small built-in mediator with the same shape (`IRequest<T>`,
`IRequestHandler<T,TResponse>`, `ISender`, `IPipelineBehavior<T,TResponse>`)
and Mapster for mapping. Nothing here puts a license obligation on your
project.

---

## Iranian localization helpers

Small, zero-dependency, opt-in helpers in `BaseRepository.Domain.Common`. They
are not wired into anything by default — use what you need. (For phone numbers
from any country, use the `IPhoneNumberValidator` described under
Authentication instead; these are Iran-only on purpose.)

- **`IranianCalendar`** — `ToShamsi` / `FromShamsi` / `ToHijri` / `FromHijri`
  convert between `DateTime` and Shamsi/Hijri `(Year, Month, Day)` tuples.
  Note that `HijriCalendar` uses a fixed tabular algorithm, not Umm al-Qura, so
  it can be a day off from observation-based religious dates.
- **`IranianNationalCode.IsValid(string)`** — validates a national ID's (کد ملی)
  checksum, and rejects all-same-digit strings which pass the checksum by
  construction but are never real.
- **`PersianMobileNumber.IsValid` / `.Normalize`** — accepts `+98`, `0098`,
  `98`, and `0` prefixes and normalizes them all to `09XXXXXXXXX`.

The last two are also available as FluentValidation rules:

```csharp
RuleFor(x => x.Phone).PersianMobileNumber();
RuleFor(x => x.NationalCode).IranianNationalCode();
```

---

## Running the tests

```bash
dotnet test
```

Each layer has its own test project — unit tests for Domain and Application,
integration tests against a real SQLite database for Infrastructure, and
functional tests for the API that drive the real `Program.cs` pipeline over
real HTTP.

---

## Solution layout

```
src/
  Domain/           zero dependencies
  Application/      depends on Domain
  Infrastructure/   depends on Application
  Api/              depends on Application + Infrastructure
tests/
  *.Domain.UnitTests
  *.Application.UnitTests
  *.Infrastructure.IntegrationTests
  *.Api.FunctionalTests
```

---

## License

MIT — see [LICENSE](https://github.com/alireza-flhz/CleanArchitecture-Api-Template/blob/master/LICENSE).

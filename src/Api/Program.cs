using System.Text;
using Asp.Versioning;
using BaseRepository.Api.ExceptionHandling;
using BaseRepository.Api.Services;
using BaseRepository.Application;
using BaseRepository.Application.Abstractions;
using BaseRepository.Application.TodoItems;
using BaseRepository.Domain.Entities;
using BaseRepository.Infrastructure;
using BaseRepository.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();

// ICurrentUser (Auth's profile endpoints) needs HttpContext, which is an ASP.NET Core concern -
// implemented here in Api, not Infrastructure, to keep Infrastructure free of hosting deps.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// The template's example entity - see TodoItem's doc comment for what to delete once you
// don't need it any more. Read lazily for the same reason the JWT signing key is (see below).
builder.Services.AddPersistence<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=app.db"));
builder.Services.AddCrudHandlers<TodoItem, int, TodoItemDto>();

builder.Services.AddControllers();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOutputCache();

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

// One OpenAPI document per API version, served at /openapi/{name}.json. ApiExplorer's
// GroupNameFormat above stamps each endpoint with "v1", and the document named "v1" picks
// up exactly those endpoints. Registered by hand rather than through
// Asp.Versioning.OpenApi's WithDocumentPerVersion(), which ships no asset for anything
// below the newest framework - this keeps one code path everywhere. Adding v2 later means
// adding AddOpenApi("v2") here.
builder.Services.AddOpenApi("v1");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Read lazily (not into a variable captured before Build()) so this reflects the
        // final, fully-merged configuration - including overrides a test host applies via
        // WebApplicationFactory, which land after this file's top-level statements start
        // running but before the options are actually resolved per-request.
        var jwtSection = builder.Configuration.GetSection("Jwt");
        var signingKey = jwtSection["SigningKey"];

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"] ?? "BaseRepository.Api",
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"] ?? "BaseRepository.Api",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = string.IsNullOrEmpty(signingKey)
                ? null
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// No migrations tooling required to just run the template: create the schema if it isn't
// there yet. Switch to EF Core migrations (dotnet ef migrations add ...) once you outgrow this.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

app.UseSerilogRequestLogging();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.UseOutputCache();

app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "BaseRepository.Api", status = "running" }));
app.MapControllers();

app.MapOpenApi();
app.MapScalarApiReference();

app.Run();

public partial class Program
{
}

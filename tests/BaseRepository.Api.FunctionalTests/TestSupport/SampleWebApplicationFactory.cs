using System;
using System.Collections.Generic;
using System.IO;
using BaseRepository.Application;
using BaseRepository.Application.Cqrs.Commands;
using BaseRepository.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BaseRepository.Api.FunctionalTests.TestSupport;

/// <summary>
/// Wires a real SQLite-backed entity (SampleEntity/SampleDto/SamplesController) into the
/// shipped Api pipeline, so these tests exercise the whole stack - real HTTP, real
/// controller, real mediator/validation pipeline, real EF Core, real exception mapping -
/// not just each layer in isolation.
/// </summary>
public class SampleWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    // Program.cs's own AppDbContext (the TodoItem sample) still spins up in every test host
    // built from this factory, even for tests that have nothing to do with it. Point it at an
    // isolated temp file per factory instance instead of the shared default app.db, so parallel
    // test classes (each gets its own factory) don't collide on the same SQLite file.
    private readonly string _appDbPath = Path.Combine(Path.GetTempPath(), $"basecrud-apptests-{Guid.NewGuid():N}.db");

    public SampleWebApplicationFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = TestJwt.Issuer,
                ["Jwt:Audience"] = TestJwt.Audience,
                ["Jwt:SigningKey"] = TestJwt.SigningKey,
                ["ConnectionStrings:Default"] = $"Data Source={_appDbPath}"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(SamplesController).Assembly);

            services.AddPersistence<SampleDbContext>(options => options.UseSqlite(_connection));
            services.AddCrudHandlers<SampleEntity, int, SampleDto>();
            services.AddScoped<IValidator<CreateCommand<SampleEntity, int, SampleDto>>, SampleCreateValidator>();

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<SampleDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _connection.Dispose();

        // See RealAppWebApplicationFactory.Dispose - pooled SQLite handles keep the file open
        // on Windows, where Delete would otherwise throw and fail the test class.
        SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(_appDbPath);
        }
        catch (IOException)
        {
        }
    }
}

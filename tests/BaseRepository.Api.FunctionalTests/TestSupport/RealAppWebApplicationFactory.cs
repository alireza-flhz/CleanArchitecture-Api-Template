using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace BaseRepository.Api.FunctionalTests.TestSupport;

/// <summary>
/// Unlike SampleWebApplicationFactory, this does NOT add any test-only wiring - it runs
/// Program.cs completely unmodified, only swapping the SQLite file for an isolated temp one
/// and supplying a JWT signing key, so these tests prove the template's actual out-of-the-box
/// TodoItem sample works, not a parallel test double of it.
/// </summary>
public class RealAppWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _appDbPath = Path.Combine(Path.GetTempPath(), $"basecrud-realapp-{Guid.NewGuid():N}.db");

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
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        // Microsoft.Data.Sqlite pools connections, so the file stays open after the host is
        // disposed. Unix happily deletes an open file; Windows throws IOException and fails
        // the whole test class. Drop the pooled handles first, and never let cleanup itself
        // fail a run - a leftover temp file is harmless.
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

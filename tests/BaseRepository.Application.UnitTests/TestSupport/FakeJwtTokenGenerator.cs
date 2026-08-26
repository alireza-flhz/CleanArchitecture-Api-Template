using System;
using BaseRepository.Application.Abstractions;
using BaseRepository.Domain.Entities;

namespace BaseRepository.Application.UnitTests.TestSupport;

public class FakeJwtTokenGenerator : IJwtTokenGenerator
{
    /// <summary>
    /// Set to simulate token issuing failing after the user has been inserted - the real
    /// generator throws when Jwt:SigningKey is not configured.
    /// </summary>
    public Exception? ThrowOnGenerate { get; set; }

    public (string Token, DateTimeOffset ExpiresAt) GenerateToken(User user)
    {
        if (ThrowOnGenerate is not null)
            throw ThrowOnGenerate;

        return ($"token-for-{user.Id}", DateTimeOffset.UtcNow.AddHours(1));
    }
}

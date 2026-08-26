using System.Threading;
using System.Threading.Tasks;
using BaseRepository.Application.Abstractions;
using BaseRepository.Application.Messaging;
using BaseRepository.Domain.Entities;
using BaseRepository.Domain.Exceptions;

namespace BaseRepository.Application.Auth;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResultDto>
{
    private readonly IRepository<User, int> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public RegisterCommandHandler(
        IRepository<User, int> repository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthResultDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _repository.AnyAsync(new UserByEmailSpecification(email), cancellationToken))
            throw new ConflictException($"A user with email \"{email}\" already exists.");

        var user = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password)
        };

        // The token needs the identity the database assigns, so it can only be issued after
        // the insert - and issuing it can fail (an unconfigured signing key, for one). Without
        // a transaction that leaves a registered user who never received a token and whose
        // email is now taken, with no way to log in. Commit only once the token exists;
        // disposing an uncommitted transaction rolls the insert back.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        await _repository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(user);

        await transaction.CommitAsync(cancellationToken);

        return new AuthResultDto
        {
            UserId = user.Id,
            Email = user.Email,
            Token = token,
            ExpiresAt = expiresAt
        };
    }
}

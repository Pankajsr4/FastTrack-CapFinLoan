using CapFinLoan.Auth.Application.Contracts.Requests;
using CapFinLoan.Auth.Application.Contracts.Responses;
using CapFinLoan.Auth.Application.Interfaces;
using CapFinLoan.Auth.Domain.Constants;
using CapFinLoan.Auth.Domain.Entities;
using CapFinLoan.Messaging.Contracts.Events;

namespace CapFinLoan.Auth.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEventPublisher _eventPublisher;

    public AuthService(IUserRepository userRepository, IJwtTokenGenerator jwtTokenGenerator, IEventPublisher eventPublisher)
    {
        _userRepository = userRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _eventPublisher = eventPublisher;
    }

    public async Task<AuthResponse> SignupAsync(SignupRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _userRepository.ExistsByEmailAsync(email, cancellationToken))
        {
            throw new InvalidOperationException("An account already exists with this email.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            PhoneNumber = request.Phone.Trim(),
            Name = request.Name.Trim(),
            Role = RoleNames.Applicant,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(user, request.Password, cancellationToken);

        try
        {
            await _eventPublisher.PublishAsync(new UserRegisteredEvent
            {
                UserId = user.Id,
                Email = user.Email!,
                FullName = user.Name,
                Role = user.Role,
                RegisteredAtUtc = user.CreatedAtUtc
            }, cancellationToken);
        }
        catch { /* RabbitMQ unavailable — registration still succeeds */ }

        var (token, expiresAtUtc) = _jwtTokenGenerator.GenerateToken(user);
        return new AuthResponse
        {
            Token        = token,
            ExpiresAtUtc = expiresAtUtc,
            Role         = NormalizeRole(user.Role),
            UserId       = user.Id,
            Name         = user.Name,
            Email        = user.Email!
        };
    }

    public async Task<AuthResponse> SignupAdminAsync(SignupRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _userRepository.ExistsByEmailAsync(email, cancellationToken))
        {
            throw new InvalidOperationException("An account already exists with this email.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            PhoneNumber = request.Phone.Trim(),
            Name = request.Name.Trim(),
            Role = RoleNames.Admin,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(user, request.Password, cancellationToken);

        try
        {
            await _eventPublisher.PublishAsync(new UserRegisteredEvent
            {
                UserId = user.Id,
                Email = user.Email!,
                FullName = user.Name,
                Role = user.Role,
                RegisteredAtUtc = user.CreatedAtUtc
            }, cancellationToken);
        }
        catch { /* RabbitMQ unavailable — registration still succeeds */ }

        var (token, expiresAtUtc) = _jwtTokenGenerator.GenerateToken(user);
        return new AuthResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            Role = NormalizeRole(user.Role),
            UserId = user.Id,
            Name = user.Name,
            Email = user.Email!
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null || !await _userRepository.CheckPasswordAsync(user, request.Password))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("User is deactivated.");
        }

        var (token, expiresAtUtc) = _jwtTokenGenerator.GenerateToken(user);
        return new AuthResponse
        {
            Token        = token,
            ExpiresAtUtc = expiresAtUtc,
            Role         = NormalizeRole(user.Role),
            UserId       = user.Id,
            Name         = user.Name,
            Email        = user.Email!
        };
    }

    public async Task<IReadOnlyCollection<UserSummaryResponse>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        return users
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(MapUser)
            .ToArray();
    }

    public async Task<UserSummaryResponse> UpdateUserStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
                   ?? throw new KeyNotFoundException("User not found.");

        user.IsActive = isActive;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        return MapUser(user);
    }

    private static UserSummaryResponse MapUser(ApplicationUser user)
    {
        return new UserSummaryResponse
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email ?? string.Empty,
            Phone = user.PhoneNumber ?? string.Empty,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAtUtc = user.CreatedAtUtc
        };
    }

    /// <summary>
    /// Normalises role to title case regardless of what is stored in the DB.
    /// "ADMIN" → "Admin", "APPLICANT" → "Applicant", "admin" → "Admin"
    /// </summary>
    private static string NormalizeRole(string role) =>
        role.ToUpperInvariant() switch
        {
            "ADMIN"     => "Admin",
            "APPLICANT" => "Applicant",
            _           => char.ToUpperInvariant(role[0]) + role[1..].ToLowerInvariant()
        };
}
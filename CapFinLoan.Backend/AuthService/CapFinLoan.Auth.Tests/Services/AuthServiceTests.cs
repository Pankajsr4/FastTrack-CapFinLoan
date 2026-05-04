using CapFinLoan.Auth.Application.Contracts.Requests;
using CapFinLoan.Auth.Application.Interfaces;
using CapFinLoan.Auth.Domain.Constants;
using CapFinLoan.Auth.Domain.Entities;
using CapFinLoan.Messaging.Contracts.Events;
using FluentAssertions;
using Moq;

namespace CapFinLoan.Auth.Tests.Services;

/// <summary>
/// Unit tests for AuthService.
/// All dependencies (IUserRepository, IJwtTokenGenerator, IEventPublisher) are mocked.
/// </summary>
public class AuthServiceTests
{
    // ── Mocks ─────────────────────────────────────────────────────────────────
    private readonly Mock<IUserRepository>    _repoMock  = new();
    private readonly Mock<IJwtTokenGenerator> _jwtMock   = new();
    private readonly Mock<IEventPublisher>    _pubMock   = new();

    private CapFinLoan.Auth.Application.Services.AuthService CreateSut() =>
        new(_repoMock.Object, _jwtMock.Object, _pubMock.Object);

    // ── Fixtures ──────────────────────────────────────────────────────────────
    private static readonly (string Token, DateTime Expires) FakeJwt =
        ("fake.jwt.token", DateTime.UtcNow.AddHours(1));

    private static SignupRequest ValidSignupRequest(string email = "user@example.com") => new()
    {
        Name     = "Priya Patel",
        Email    = email,
        Phone    = "9876543210",
        Password = "SecurePass1!"
    };

    private static LoginRequest ValidLoginRequest(string email = "user@example.com") => new()
    {
        Email    = email,
        Password = "SecurePass1!"
    };

    private static ApplicationUser ActiveUser(string email = "user@example.com") => new()
    {
        Id       = Guid.NewGuid(),
        Email    = email,
        UserName = email,
        Name     = "Priya Patel",
        Role     = RoleNames.Applicant,
        IsActive = true
    };

    // ═════════════════════════════════════════════════════════════════════════
    // SignupAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SignupAsync_NewEmail_ReturnsAuthResponseWithToken()
    {
        _repoMock.Setup(r => r.ExistsByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _jwtMock.Setup(j => j.GenerateToken(It.IsAny<ApplicationUser>()))
                .Returns(FakeJwt);
        _pubMock.Setup(p => p.PublishAsync(It.IsAny<UserRegisteredEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var result = await CreateSut().SignupAsync(ValidSignupRequest());

        result.Token.Should().Be("fake.jwt.token");
        result.Role.Should().Be("Applicant");
        result.Email.Should().Be("user@example.com");
    }

    [Fact]
    public async Task SignupAsync_NormalisesEmailToLowercase()
    {
        ApplicationUser? captured = null;
        _repoMock.Setup(r => r.ExistsByEmailAsync("upper@example.com", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .Callback<ApplicationUser, string, CancellationToken>((u, _, _) => captured = u)
                 .Returns(Task.CompletedTask);
        _jwtMock.Setup(j => j.GenerateToken(It.IsAny<ApplicationUser>())).Returns(FakeJwt);
        _pubMock.Setup(p => p.PublishAsync(It.IsAny<UserRegisteredEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        await CreateSut().SignupAsync(ValidSignupRequest("UPPER@EXAMPLE.COM"));

        captured!.Email.Should().Be("upper@example.com");
        captured.UserName.Should().Be("upper@example.com");
    }

    [Fact]
    public async Task SignupAsync_DuplicateEmail_ThrowsInvalidOperationException()
    {
        _repoMock.Setup(r => r.ExistsByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        await CreateSut()
            .Invoking(s => s.SignupAsync(ValidSignupRequest()))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task SignupAsync_AssignsApplicantRole()
    {
        ApplicationUser? captured = null;
        _repoMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .Callback<ApplicationUser, string, CancellationToken>((u, _, _) => captured = u)
                 .Returns(Task.CompletedTask);
        _jwtMock.Setup(j => j.GenerateToken(It.IsAny<ApplicationUser>())).Returns(FakeJwt);
        _pubMock.Setup(p => p.PublishAsync(It.IsAny<UserRegisteredEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        await CreateSut().SignupAsync(ValidSignupRequest());

        captured!.Role.Should().Be(RoleNames.Applicant);
    }

    [Fact]
    public async Task SignupAsync_PublishesUserRegisteredEvent()
    {
        _repoMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _jwtMock.Setup(j => j.GenerateToken(It.IsAny<ApplicationUser>())).Returns(FakeJwt);
        _pubMock.Setup(p => p.PublishAsync(It.IsAny<UserRegisteredEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        await CreateSut().SignupAsync(ValidSignupRequest());

        _pubMock.Verify(p => p.PublishAsync(
            It.Is<UserRegisteredEvent>(e => e.Email == "user@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignupAsync_RabbitMqUnavailable_StillSucceeds()
    {
        // Event publish throws — signup should still return a valid response
        _repoMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _jwtMock.Setup(j => j.GenerateToken(It.IsAny<ApplicationUser>())).Returns(FakeJwt);
        _pubMock.Setup(p => p.PublishAsync(It.IsAny<UserRegisteredEvent>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("RabbitMQ unavailable"));

        var act = () => CreateSut().SignupAsync(ValidSignupRequest());

        await act.Should().NotThrowAsync();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SignupAdminAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SignupAdminAsync_AssignsAdminRole()
    {
        ApplicationUser? captured = null;
        _repoMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .Callback<ApplicationUser, string, CancellationToken>((u, _, _) => captured = u)
                 .Returns(Task.CompletedTask);
        _jwtMock.Setup(j => j.GenerateToken(It.IsAny<ApplicationUser>())).Returns(FakeJwt);
        _pubMock.Setup(p => p.PublishAsync(It.IsAny<UserRegisteredEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var result = await CreateSut().SignupAdminAsync(ValidSignupRequest());

        captured!.Role.Should().Be(RoleNames.Admin);
        result.Role.Should().Be("Admin");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // LoginAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResponse()
    {
        var user = ActiveUser();
        _repoMock.Setup(r => r.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _repoMock.Setup(r => r.CheckPasswordAsync(user, "SecurePass1!"))
                 .ReturnsAsync(true);
        _jwtMock.Setup(j => j.GenerateToken(user)).Returns(FakeJwt);

        var result = await CreateSut().LoginAsync(ValidLoginRequest());

        result.Token.Should().Be("fake.jwt.token");
        result.Role.Should().Be("Applicant");
        result.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task LoginAsync_NormalisesEmailBeforeLookup()
    {
        var user = ActiveUser("mixed@example.com");
        _repoMock.Setup(r => r.GetByEmailAsync("mixed@example.com", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _repoMock.Setup(r => r.CheckPasswordAsync(user, It.IsAny<string>()))
                 .ReturnsAsync(true);
        _jwtMock.Setup(j => j.GenerateToken(user)).Returns(FakeJwt);

        await CreateSut().LoginAsync(new() { Email = "MIXED@EXAMPLE.COM", Password = "pass" });

        _repoMock.Verify(r => r.GetByEmailAsync("mixed@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ThrowsUnauthorizedAccessException()
    {
        _repoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((ApplicationUser?)null);

        await CreateSut()
            .Invoking(s => s.LoginAsync(ValidLoginRequest()))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorizedAccessException()
    {
        var user = ActiveUser();
        _repoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _repoMock.Setup(r => r.CheckPasswordAsync(user, It.IsAny<string>()))
                 .ReturnsAsync(false);

        await CreateSut()
            .Invoking(s => s.LoginAsync(ValidLoginRequest()))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task LoginAsync_DeactivatedUser_ThrowsUnauthorizedAccessException()
    {
        var user = ActiveUser();
        user.IsActive = false;
        _repoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _repoMock.Setup(r => r.CheckPasswordAsync(user, It.IsAny<string>()))
                 .ReturnsAsync(true);

        await CreateSut()
            .Invoking(s => s.LoginAsync(ValidLoginRequest()))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*deactivated*");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // UpdateUserStatusAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateUserStatusAsync_UserExists_UpdatesIsActive()
    {
        var user = ActiveUser();
        _repoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var result = await CreateSut().UpdateUserStatusAsync(user.Id, false);

        result.IsActive.Should().BeFalse();
        _repoMock.Verify(r => r.UpdateAsync(
            It.Is<ApplicationUser>(u => u.IsActive == false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((ApplicationUser?)null);

        await CreateSut()
            .Invoking(s => s.UpdateUserStatusAsync(Guid.NewGuid(), true))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*User not found*");
    }
}

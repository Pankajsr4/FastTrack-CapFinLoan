using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CapFinLoan.Auth.Domain.Constants;
using CapFinLoan.Auth.Domain.Entities;
using CapFinLoan.Auth.Infrastructure.Configuration;
using CapFinLoan.Auth.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace CapFinLoan.Auth.Tests.Security;

/// <summary>
/// Tests for JwtTokenGenerator — verifies claims, expiry, and signing.
/// No mocks needed: this is a pure function over configuration.
/// </summary>
public class JwtTokenGeneratorTests
{
    private static JwtTokenGenerator CreateSut(int expiryMinutes = 60) =>
        new(Options.Create(new JwtOptions
        {
            Key           = "CapFinLoan.Test.Jwt.Signing.Key.AtLeast32Chars!",
            Issuer        = "CapFinLoan.Test",
            Audience      = "CapFinLoan.TestAudience",
            ExpiryMinutes = expiryMinutes
        }));

    private static ApplicationUser SampleUser(string role = RoleNames.Applicant) => new()
    {
        Id       = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Email    = "test@example.com",
        UserName = "test@example.com",
        Name     = "Test User",
        Role     = role
    };

    // ── Token structure ───────────────────────────────────────────────────────

    [Fact]
    public void GenerateToken_ReturnsNonEmptyToken()
    {
        var (token, _) = CreateSut().GenerateToken(SampleUser());
        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateToken_TokenHasThreeParts()
    {
        var (token, _) = CreateSut().GenerateToken(SampleUser());
        token.Split('.').Should().HaveCount(3, "JWT must be header.payload.signature");
    }

    // ── Claims ────────────────────────────────────────────────────────────────

    [Fact]
    public void GenerateToken_ContainsSubClaim_EqualToUserId()
    {
        var user = SampleUser();
        var (token, _) = CreateSut().GenerateToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt     = handler.ReadJwtToken(token);

        jwt.Subject.Should().Be(user.Id.ToString());
    }

    [Fact]
    public void GenerateToken_ContainsEmailClaim()
    {
        var user = SampleUser();
        var (token, _) = CreateSut().GenerateToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
    }

    [Fact]
    public void GenerateToken_ContainsRoleClaim_ForApplicant()
    {
        var (token, _) = CreateSut().GenerateToken(SampleUser(RoleNames.Applicant));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Claims.Should().Contain(c =>
            c.Type == ClaimTypes.Role && c.Value == RoleNames.Applicant);
    }

    [Fact]
    public void GenerateToken_ContainsRoleClaim_ForAdmin()
    {
        var (token, _) = CreateSut().GenerateToken(SampleUser(RoleNames.Admin));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Claims.Should().Contain(c =>
            c.Type == ClaimTypes.Role && c.Value == RoleNames.Admin);
    }

    [Fact]
    public void GenerateToken_ContainsNameClaim()
    {
        var user = SampleUser();
        var (token, _) = CreateSut().GenerateToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == user.Name);
    }

    // ── Expiry ────────────────────────────────────────────────────────────────

    [Fact]
    public void GenerateToken_ExpiresAtUtc_IsApproximatelyNowPlusExpiryMinutes()
    {
        var expiryMinutes = 120;
        var before = DateTime.UtcNow;
        var (_, expiresAt) = CreateSut(expiryMinutes).GenerateToken(SampleUser());
        var after = DateTime.UtcNow;

        expiresAt.Should().BeOnOrAfter(before.AddMinutes(expiryMinutes - 1));
        expiresAt.Should().BeOnOrBefore(after.AddMinutes(expiryMinutes + 1));
    }

    [Fact]
    public void GenerateToken_TokenExpiry_MatchesReturnedExpiresAtUtc()
    {
        var (token, expiresAt) = CreateSut().GenerateToken(SampleUser());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Allow 5-second tolerance for test execution time
        jwt.ValidTo.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(5));
    }

    // ── Issuer / Audience ─────────────────────────────────────────────────────

    [Fact]
    public void GenerateToken_HasCorrectIssuer()
    {
        var (token, _) = CreateSut().GenerateToken(SampleUser());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Issuer.Should().Be("CapFinLoan.Test");
    }

    [Fact]
    public void GenerateToken_HasCorrectAudience()
    {
        var (token, _) = CreateSut().GenerateToken(SampleUser());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Audiences.Should().Contain("CapFinLoan.TestAudience");
    }

    // ── Different users produce different tokens ──────────────────────────────

    [Fact]
    public void GenerateToken_DifferentUsers_ProduceDifferentTokens()
    {
        var sut   = CreateSut();
        var user1 = SampleUser();
        var user2 = new ApplicationUser
        {
            Id = Guid.NewGuid(), Email = "other@example.com",
            UserName = "other@example.com", Name = "Other", Role = RoleNames.Applicant
        };

        var (t1, _) = sut.GenerateToken(user1);
        var (t2, _) = sut.GenerateToken(user2);

        t1.Should().NotBe(t2);
    }
}

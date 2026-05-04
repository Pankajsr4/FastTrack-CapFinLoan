using CapFinLoan.Api.Shared.Caching;
using CapFinLoan.Application.Application.Interfaces;
using CapFinLoan.Application.Application.Services;
using CapFinLoan.Application.Domain.Constants;
using CapFinLoan.Application.Domain.Entities;
using CapFinLoan.Application.Tests.Helpers;
using CapFinLoan.Messaging.Contracts.Events;
using FluentAssertions;
using Moq;

namespace CapFinLoan.Application.Tests.Services;

/// <summary>
/// Unit tests for LoanApplicationService.
/// All dependencies are mocked — no DB, no HTTP, no RabbitMQ.
/// </summary>
public class LoanApplicationServiceTests
{
    // ── Mocks ─────────────────────────────────────────────────────────────────
    private readonly Mock<ILoanApplicationRepository> _repoMock   = new();
    private readonly Mock<IEventPublisher>            _pubMock    = new();
    private readonly Mock<ICacheService>              _cacheMock  = new();

    private LoanApplicationService CreateSut() =>
        new(_repoMock.Object, _pubMock.Object, _cacheMock.Object);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetupRepoGetById(LoanApplication? app) =>
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(app);

    private void SetupCacheGetNull<T>() where T : class =>
        _cacheMock.Setup(c => c.GetAsync<T>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((T?)null);

    // ═════════════════════════════════════════════════════════════════════════
    // CreateDraftAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateDraftAsync_ValidRequest_ReturnsDraftResponse()
    {
        // Arrange
        var sut     = CreateSut();
        var userId  = TestDataBuilder.DefaultUserId;
        var request = TestDataBuilder.ValidRequest();

        _repoMock.Setup(r => r.AddAsync(It.IsAny<LoanApplication>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        // Act
        var result = await sut.CreateDraftAsync(userId, request);

        // Assert
        result.Status.Should().Be(ApplicationStatuses.Draft);
        result.ApplicantUserId.Should().Be(userId);
        result.PersonalDetails.FirstName.Should().Be("Arjun");
        result.PersonalDetails.Email.Should().Be("arjun.sharma@example.com");
        result.LoanDetails.RequestedAmount.Should().Be(500_000m);
        result.ApplicationNumber.Should().StartWith("APP-");
    }

    [Fact]
    public async Task CreateDraftAsync_CallsRepositoryAddOnce()
    {
        var sut = CreateSut();
        _repoMock.Setup(r => r.AddAsync(It.IsAny<LoanApplication>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        await sut.CreateDraftAsync(TestDataBuilder.DefaultUserId, TestDataBuilder.ValidRequest());

        _repoMock.Verify(r => r.AddAsync(It.IsAny<LoanApplication>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateDraftAsync_DoesNotPublishEvent()
    {
        // Draft creation should NOT publish any event
        var sut = CreateSut();
        _repoMock.Setup(r => r.AddAsync(It.IsAny<LoanApplication>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        await sut.CreateDraftAsync(TestDataBuilder.DefaultUserId, TestDataBuilder.ValidRequest());

        _pubMock.Verify(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateDraftAsync_AddsInitialStatusHistoryEntry()
    {
        LoanApplication? captured = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<LoanApplication>(), It.IsAny<CancellationToken>()))
                 .Callback<LoanApplication, CancellationToken>((app, _) => captured = app)
                 .Returns(Task.CompletedTask);

        await CreateSut().CreateDraftAsync(TestDataBuilder.DefaultUserId, TestDataBuilder.ValidRequest());

        captured.Should().NotBeNull();
        captured!.StatusHistory.Should().HaveCount(1);
        captured.StatusHistory.First().ToStatus.Should().Be(ApplicationStatuses.Draft);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SubmitAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SubmitAsync_ValidDraft_ChangesStatusToSubmitted()
    {
        var app = TestDataBuilder.ValidDraftApplication();
        SetupRepoGetById(app);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<LoanApplication>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);
        _pubMock.Setup(p => p.PublishAsync(It.IsAny<ApplicationSubmittedEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var result = await CreateSut().SubmitAsync(app.Id, app.ApplicantUserId);

        result.Status.Should().Be(ApplicationStatuses.Submitted);
        result.Id.Should().Be(app.Id);
    }

    [Fact]
    public async Task SubmitAsync_ValidDraft_PublishesApplicationSubmittedEvent()
    {
        var app = TestDataBuilder.ValidDraftApplication();
        SetupRepoGetById(app);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<LoanApplication>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);
        _pubMock.Setup(p => p.PublishAsync(It.IsAny<ApplicationSubmittedEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        await CreateSut().SubmitAsync(app.Id, app.ApplicantUserId);

        _pubMock.Verify(
            p => p.PublishAsync(
                It.Is<ApplicationSubmittedEvent>(e =>
                    e.ApplicationId == app.Id &&
                    e.ApplicantUserId == app.ApplicantUserId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_AlreadySubmitted_ThrowsInvalidOperationException()
    {
        var app = TestDataBuilder.ValidDraftApplication();
        app.Status = ApplicationStatuses.Submitted;
        SetupRepoGetById(app);

        var sut = CreateSut();
        await sut.Invoking(s => s.SubmitAsync(app.Id, app.ApplicantUserId))
                 .Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*Only draft applications can be submitted*");
    }

    [Fact]
    public async Task SubmitAsync_IncompleteApplication_ThrowsInvalidOperationException()
    {
        var app = TestDataBuilder.IncompleteDraftApplication();
        SetupRepoGetById(app);

        var sut = CreateSut();
        await sut.Invoking(s => s.SubmitAsync(app.Id, app.ApplicantUserId))
                 .Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*incomplete*");
    }

    [Fact]
    public async Task SubmitAsync_AmountTooLow_ThrowsInvalidOperationException()
    {
        var app = TestDataBuilder.ValidDraftApplication(amount: 1_000m); // below 10,000 minimum
        SetupRepoGetById(app);

        await CreateSut()
            .Invoking(s => s.SubmitAsync(app.Id, app.ApplicantUserId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*10,000*");
    }

    [Fact]
    public async Task SubmitAsync_AmountTooHigh_ThrowsInvalidOperationException()
    {
        var app = TestDataBuilder.ValidDraftApplication(amount: 6_000_000m); // above 5,000,000 max
        SetupRepoGetById(app);

        await CreateSut()
            .Invoking(s => s.SubmitAsync(app.Id, app.ApplicantUserId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*5,000,000*");
    }

    [Fact]
    public async Task SubmitAsync_TenureTooShort_ThrowsInvalidOperationException()
    {
        var app = TestDataBuilder.ValidDraftApplication(tenureMonths: 3); // below 6 minimum
        SetupRepoGetById(app);

        await CreateSut()
            .Invoking(s => s.SubmitAsync(app.Id, app.ApplicantUserId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*6*");
    }

    [Fact]
    public async Task SubmitAsync_WrongOwner_ThrowsUnauthorizedAccessException()
    {
        var app = TestDataBuilder.ValidDraftApplication();
        SetupRepoGetById(app);
        var differentUser = Guid.NewGuid();

        await CreateSut()
            .Invoking(s => s.SubmitAsync(app.Id, differentUser))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SubmitAsync_ApplicationNotFound_ThrowsKeyNotFoundException()
    {
        SetupRepoGetById(null);

        await CreateSut()
            .Invoking(s => s.SubmitAsync(Guid.NewGuid(), Guid.NewGuid()))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task SubmitAsync_IncomeEqualsEmi_ThrowsInvalidOperationException()
    {
        var app = TestDataBuilder.ValidDraftApplication();
        app.MonthlyIncome     = 10_000m;
        app.ExistingEmiAmount = 10_000m; // income == EMI — not allowed
        SetupRepoGetById(app);

        await CreateSut()
            .Invoking(s => s.SubmitAsync(app.Id, app.ApplicantUserId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Monthly income must be greater than existing EMI*");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // WithdrawAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(ApplicationStatuses.Draft)]
    [InlineData(ApplicationStatuses.Submitted)]
    [InlineData(ApplicationStatuses.DocsPending)]
    [InlineData(ApplicationStatuses.DocsVerified)]
    public async Task WithdrawAsync_WithdrawableStatus_ChangesStatusToWithdrawn(string status)
    {
        var app = TestDataBuilder.ValidDraftApplication();
        app.Status = status;
        SetupRepoGetById(app);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<LoanApplication>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        var result = await CreateSut().WithdrawAsync(
            app.Id, app.ApplicantUserId,
            new() { Reason = "Changed my mind" });

        result.Status.Should().Be(ApplicationStatuses.Withdrawn);
    }

    [Theory]
    [InlineData(ApplicationStatuses.UnderReview)]
    [InlineData(ApplicationStatuses.Approved)]
    [InlineData(ApplicationStatuses.Rejected)]
    public async Task WithdrawAsync_NonWithdrawableStatus_ThrowsInvalidOperationException(string status)
    {
        var app = TestDataBuilder.ValidDraftApplication();
        app.Status = status;
        SetupRepoGetById(app);

        await CreateSut()
            .Invoking(s => s.WithdrawAsync(app.Id, app.ApplicantUserId, new()))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be withdrawn*");
    }

    [Fact]
    public async Task WithdrawAsync_WithReason_StoresReasonOnApplication()
    {
        var app = TestDataBuilder.ValidDraftApplication();
        LoanApplication? saved = null;
        SetupRepoGetById(app);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<LoanApplication>(), It.IsAny<CancellationToken>()))
                 .Callback<LoanApplication, CancellationToken>((a, _) => saved = a)
                 .Returns(Task.CompletedTask);
        _cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        await CreateSut().WithdrawAsync(app.Id, app.ApplicantUserId, new() { Reason = "No longer needed" });

        saved!.WithdrawalReason.Should().Be("No longer needed");
        saved.WithdrawnAtUtc.Should().NotBeNull();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetMineAsync — cache behaviour
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetMineAsync_CacheHit_DoesNotCallRepository()
    {
        var userId = TestDataBuilder.DefaultUserId;
        var cached = new[] { new CapFinLoan.Application.Application.Contracts.Responses.LoanApplicationResponse() };
        _cacheMock.Setup(c => c.GetAsync<CapFinLoan.Application.Application.Contracts.Responses.LoanApplicationResponse[]>(
                      It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(cached);

        var result = await CreateSut().GetMineAsync(userId);

        result.Should().HaveCount(1);
        _repoMock.Verify(r => r.GetByApplicantUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMineAsync_CacheMiss_CallsRepositoryAndPopulatesCache()
    {
        var userId = TestDataBuilder.DefaultUserId;
        var apps   = new[] { TestDataBuilder.ValidDraftApplication(userId: userId) };

        SetupCacheGetNull<CapFinLoan.Application.Application.Contracts.Responses.LoanApplicationResponse[]>();
        _repoMock.Setup(r => r.GetByApplicantUserIdAsync(userId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(apps);
        _cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        var result = await CreateSut().GetMineAsync(userId);

        result.Should().HaveCount(1);
        _repoMock.Verify(r => r.GetByApplicantUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.SetAsync(
            $"apps:user:{userId}",
            It.IsAny<object>(),
            TimeSpan.FromMinutes(5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // DeleteDraftAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteDraftAsync_DraftStatus_CallsRepositoryDelete()
    {
        var app = TestDataBuilder.ValidDraftApplication();
        SetupRepoGetById(app);
        _repoMock.Setup(r => r.DeleteAsync(It.IsAny<LoanApplication>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        await CreateSut().DeleteDraftAsync(app.Id, app.ApplicantUserId, false);

        _repoMock.Verify(r => r.DeleteAsync(app, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteDraftAsync_SubmittedStatus_ThrowsInvalidOperationException()
    {
        var app = TestDataBuilder.ValidDraftApplication();
        app.Status = ApplicationStatuses.Submitted;
        SetupRepoGetById(app);

        await CreateSut()
            .Invoking(s => s.DeleteDraftAsync(app.Id, app.ApplicantUserId, false))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only draft applications can be deleted*");
    }
}

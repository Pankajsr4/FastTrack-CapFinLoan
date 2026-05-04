using CapFinLoan.Application.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapFinLoan.Application.Persistence.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<LoanApplication> LoanApplications => Set<LoanApplication>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories => Set<ApplicationStatusHistory>();
    public DbSet<ApplicationHistory> ApplicationHistories => Set<ApplicationHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");

        modelBuilder.Entity<LoanApplication>(entity =>
        {
            entity.ToTable("LoanApplications");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ApplicationNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(100);
            entity.Property(x => x.LastName).HasMaxLength(100);
            entity.Property(x => x.Gender).HasMaxLength(20);
            entity.Property(x => x.Email).HasMaxLength(150);
            entity.Property(x => x.Phone).HasMaxLength(20);
            entity.Property(x => x.AddressLine1).HasMaxLength(250);
            entity.Property(x => x.AddressLine2).HasMaxLength(250);
            entity.Property(x => x.City).HasMaxLength(100);
            entity.Property(x => x.State).HasMaxLength(100);
            entity.Property(x => x.PostalCode).HasMaxLength(15);
            entity.Property(x => x.EmployerName).HasMaxLength(150);
            entity.Property(x => x.EmploymentType).HasMaxLength(50);
            entity.Property(x => x.MonthlyIncome).HasColumnType("decimal(18,2)");
            entity.Property(x => x.AnnualIncome).HasColumnType("decimal(18,2)");
            entity.Property(x => x.ExistingEmiAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.RequestedAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.LoanPurpose).HasMaxLength(200);
            entity.Property(x => x.Remarks).HasMaxLength(1000);

            // Edit tracking
            entity.Property(x => x.LastModifiedBy).HasMaxLength(100);
            entity.Property(x => x.WithdrawalReason).HasMaxLength(1000);

            entity.HasIndex(x => x.ApplicationNumber).IsUnique();

            entity.HasMany(x => x.StatusHistory)
                .WithOne(x => x.LoanApplication)
                .HasForeignKey(x => x.LoanApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.History)
                .WithOne(x => x.LoanApplication)
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApplicationStatusHistory>(entity =>
        {
            entity.ToTable("ApplicationStatusHistories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FromStatus).HasMaxLength(30);
            entity.Property(x => x.ToStatus).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Remarks).HasMaxLength(1000);
        });

        modelBuilder.Entity<ApplicationHistory>(entity =>
        {
            entity.ToTable("ApplicationHistories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(50).IsRequired();
            entity.Property(x => x.OldData).HasColumnType("nvarchar(max)");
            entity.Property(x => x.NewData).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => x.ApplicationId);
        });
    }
}
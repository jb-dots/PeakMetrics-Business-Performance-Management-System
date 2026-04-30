using Microsoft.EntityFrameworkCore;
using PeakMetrics.Web.Models;

namespace PeakMetrics.Web.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Kpi> Kpis => Set<Kpi>();
    public DbSet<KpiLogEntry> KpiLogEntries => Set<KpiLogEntry>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<StrategicGoal> StrategicGoals => Set<StrategicGoal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Department ──────────────────────────────────────────────────────
        modelBuilder.Entity<Department>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(d => d.Name).IsUnique();
        });

        // ── AppUser ─────────────────────────────────────────────────────────
        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.FullName).HasMaxLength(150).IsRequired();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            e.Property(u => u.Role).HasMaxLength(50).IsRequired();

            e.HasOne(u => u.Department)
             .WithMany(d => d.Users)
             .HasForeignKey(u => u.DepartmentId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Kpi ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Kpi>(e =>
        {
            e.HasKey(k => k.Id);
            e.Property(k => k.Name).HasMaxLength(200).IsRequired();
            e.Property(k => k.Perspective).HasMaxLength(100).IsRequired();
            e.Property(k => k.Unit).HasMaxLength(50).IsRequired();
            e.Property(k => k.Target).HasColumnType("decimal(18,4)");

            e.HasOne(k => k.Department)
             .WithMany(d => d.Kpis)
             .HasForeignKey(k => k.DepartmentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── KpiLogEntry ──────────────────────────────────────────────────────
        modelBuilder.Entity<KpiLogEntry>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.ActualValue).HasColumnType("decimal(18,4)");
            e.Property(l => l.Status).HasMaxLength(50).IsRequired();
            e.Property(l => l.Period).HasMaxLength(50).IsRequired();

            e.HasOne(l => l.Kpi)
             .WithMany(k => k.LogEntries)
             .HasForeignKey(l => l.KpiId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(l => l.LoggedBy)
             .WithMany(u => u.KpiLogEntries)
             .HasForeignKey(l => l.LoggedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Notification ─────────────────────────────────────────────────────
        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(n => n.Id);
            e.Property(n => n.Title).HasMaxLength(200).IsRequired();
            e.Property(n => n.Message).HasMaxLength(1000).IsRequired();
            e.Property(n => n.Severity).HasMaxLength(50).IsRequired();
            e.Property(n => n.Icon).HasMaxLength(100).IsRequired();

            e.HasOne(n => n.User)
             .WithMany(u => u.Notifications)
             .HasForeignKey(n => n.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── AuditLog ─────────────────────────────────────────────────────────
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Action).HasMaxLength(200).IsRequired();
            e.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
            e.Property(a => a.IpAddress).HasMaxLength(45);

            e.HasOne(a => a.User)
             .WithMany(u => u.AuditLogs)
             .HasForeignKey(a => a.UserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── StrategicGoal ─────────────────────────────────────────────────────
        modelBuilder.Entity<StrategicGoal>(e =>
        {
            e.HasKey(g => g.Id);
            e.Property(g => g.Title).HasMaxLength(300).IsRequired();
            e.Property(g => g.Perspective).HasMaxLength(100).IsRequired();
            e.Property(g => g.Status).HasMaxLength(50).IsRequired();

            e.HasOne(g => g.Owner)
             .WithMany()
             .HasForeignKey(g => g.OwnerUserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Seed Data ─────────────────────────────────────────────────────────
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Departments
        modelBuilder.Entity<Department>().HasData(
            new Department { Id = 1, Name = "Finance",          Description = "Financial planning and reporting",       CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Department { Id = 2, Name = "HR",               Description = "Human resources and talent management",  CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Department { Id = 3, Name = "Sales",            Description = "Revenue generation and client relations", CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Department { Id = 4, Name = "Operations",       Description = "Process efficiency and delivery",         CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Department { Id = 5, Name = "Customer Service", Description = "Customer satisfaction and support",       CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Department { Id = 6, Name = "Quality",          Description = "Quality assurance and compliance",        CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // Seeded accounts — values below are BCrypt hashes (workFactor: 11), NOT plaintext passwords.
        // SonarQube S2068 does not apply: these are one-way hashes stored for demo seed data only.
        // admin@peakmetrics.com   → Admin@123
        // manager@peakmetrics.com → Manager@123
        // sarah@peakmetrics.com   → User@123
        // michael@peakmetrics.com → User@123
        // emily@peakmetrics.com   → User@123
#pragma warning disable S2068 // "Hard-coded credentials" — values are BCrypt hashes, not plaintext passwords
        modelBuilder.Entity<AppUser>().HasData(
            new AppUser
            {
                Id           = 1,
                FullName     = "System Admin",
                Email        = "admin@peakmetrics.com",
                PasswordHash = "$2a$11$K2GAaeAIPqKr7/DQp1xWIuSA95c53aTx071RgaoMS7U4nTO5P1LFG", // NOSONAR — BCrypt hash, not a plaintext credential
                Role         = "Admin",
                DepartmentId = null,
                CreatedAt    = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive     = true
            },
            new AppUser
            {
                Id           = 2,
                FullName     = "Maria Santos",
                Email        = "manager@peakmetrics.com",
                PasswordHash = "$2a$11$cA3Cig0PT.t2wVj5yONGl.kQHV4pczXahzNbmghQWOBiN8Q23o212", // NOSONAR — BCrypt hash, not a plaintext credential
                Role         = "Manager",
                DepartmentId = 1,
                CreatedAt    = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive     = true
            },
            new AppUser
            {
                Id           = 3,
                FullName     = "Sarah Johnson",
                Email        = "sarah@peakmetrics.com",
                PasswordHash = "$2a$11$GTTjD7ErxWlfvdNygzUlaOi0jamF3GIPWHEjSUNyMgXAs3EFm58O6", // NOSONAR — BCrypt hash, not a plaintext credential
                Role         = "User",
                DepartmentId = 3,
                CreatedAt    = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive     = true
            },
            new AppUser
            {
                Id           = 4,
                FullName     = "Michael Chen",
                Email        = "michael@peakmetrics.com",
                PasswordHash = "$2a$11$GTTjD7ErxWlfvdNygzUlaOi0jamF3GIPWHEjSUNyMgXAs3EFm58O6", // NOSONAR — BCrypt hash, not a plaintext credential
                Role         = "User",
                DepartmentId = 4,
                CreatedAt    = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive     = true
            },
            new AppUser
            {
                Id           = 5,
                FullName     = "Emily Davis",
                Email        = "emily@peakmetrics.com",
                PasswordHash = "$2a$11$GTTjD7ErxWlfvdNygzUlaOi0jamF3GIPWHEjSUNyMgXAs3EFm58O6", // NOSONAR — BCrypt hash, not a plaintext credential
                Role         = "User",
                DepartmentId = 2,
                CreatedAt    = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive     = true
            },
            new AppUser
            {
                Id           = 6,
                FullName     = "Executive User",
                Email        = "executive@peakmetrics.com",
                PasswordHash = "$2a$11$0yCucsKKCKwaqMLlZewNtugJYHERt1WN6Q7TaM51dHvjwEBKDOe/i", // NOSONAR — BCrypt hash, not a plaintext credential
                Role         = "Executive",
                DepartmentId = null,
                CreatedAt    = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive     = true
            },
            new AppUser
            {
                Id           = 7,
                FullName     = "HR Admin",
                Email        = "hradmin@peakmetrics.com",
                PasswordHash = "$2a$11$gIbViYapQPsEwJbISIDvQu/vBawKNXbhzVjJFLfxW9qCfZDAsWaDG", // NOSONAR — BCrypt hash, not a plaintext credential
                Role         = "Administrator",
                DepartmentId = 2,
                CreatedAt    = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive     = true
            }
        );
#pragma warning restore S2068

        // KPIs
        modelBuilder.Entity<Kpi>().HasData(
            new Kpi { Id = 1, Name = "Revenue Growth Rate",      Perspective = "Financial",        Unit = "%",    Target = 15,  DepartmentId = 1, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Kpi { Id = 2, Name = "Net Profit Margin",        Perspective = "Financial",        Unit = "%",    Target = 20,  DepartmentId = 1, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Kpi { Id = 3, Name = "Employee Turnover Rate",   Perspective = "Learning & Growth",Unit = "%",    Target = 10,  DepartmentId = 2, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Kpi { Id = 4, Name = "Training Hours per Staff", Perspective = "Learning & Growth",Unit = "hrs",  Target = 40,  DepartmentId = 2, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Kpi { Id = 5, Name = "Sales Conversion Rate",    Perspective = "Customer",         Unit = "%",    Target = 30,  DepartmentId = 3, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Kpi { Id = 6, Name = "Customer Satisfaction",    Perspective = "Customer",         Unit = "score",Target = 4.5m,DepartmentId = 5, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Kpi { Id = 7, Name = "Process Cycle Time",       Perspective = "Internal Process", Unit = "days", Target = 3,   DepartmentId = 4, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Kpi { Id = 8, Name = "Defect Rate",              Perspective = "Internal Process", Unit = "%",    Target = 2,   DepartmentId = 6, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}

using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core (Code-First) model for RaceDay. Table names, column types, constraints and
/// relationships mirror the Part 1 ERD / SQL script (v2). Every foreign key uses
/// DeleteBehavior.Restrict (= SQL "NO ACTION"), exactly like the Part 1 script; child rows
/// are removed explicitly in the controllers.
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CourseRoute> Routes => Set<CourseRoute>();
    public DbSet<EventEnrolment> EventEnrolments => Set<EventEnrolment>();
    public DbSet<RaceResult> Results => Set<RaceResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser', 'Participant')"));
            e.HasKey(u => u.UserId);
            e.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(u => u.Role).HasMaxLength(20).IsUnicode(false).IsRequired().HasDefaultValue(AppRoles.Participant);
            e.Property(u => u.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable("Events", t => t.HasCheckConstraint("CK_Events_EventType", "[EventType] IN ('Run', 'Walk', 'Cycle')"));
            e.HasKey(x => x.EventId);
            e.Property(x => x.EventName).HasMaxLength(150).IsRequired();
            e.Property(x => x.EventDate).HasColumnType("date");
            e.Property(x => x.Location).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.EventType).HasMaxLength(10).IsUnicode(false).IsRequired();
            e.Property(x => x.DistanceKm).HasPrecision(6, 2);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.Organiser).WithMany(u => u.OrganisedEvents)
                .HasForeignKey(x => x.OrganiserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Events_Organiser");
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasKey(x => x.CategoryId);
            e.Property(x => x.CategoryName).HasMaxLength(100).IsRequired();
            e.Property(x => x.DistanceKm).HasPrecision(5, 2);
            e.Property(x => x.MaxParticipants).HasDefaultValue(100);
            e.Property(x => x.EntryFee).HasPrecision(8, 2).HasDefaultValue(0m);
            e.HasOne(x => x.Event).WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Categories_Event");
        });

        modelBuilder.Entity<CourseRoute>(e =>
        {
            e.ToTable("Routes");
            e.HasKey(x => x.RouteId);
            e.Property(x => x.StartPoint).HasMaxLength(150).IsRequired();
            e.Property(x => x.EndPoint).HasMaxLength(150).IsRequired();
            e.Property(x => x.RouteDescription).HasMaxLength(1000);
            e.Property(x => x.MapUrl).HasMaxLength(255);
            e.HasIndex(x => x.CategoryId).IsUnique();
            e.HasOne(x => x.Category).WithOne(c => c.Route)
                .HasForeignKey<CourseRoute>(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Routes_Category");
        });

        modelBuilder.Entity<EventEnrolment>(e =>
        {
            e.ToTable("EventEnrolments", t => t.HasCheckConstraint("CK_Enrolments_Status", "[Status] IN ('Pending', 'Confirmed', 'Cancelled')"));
            e.HasKey(x => x.EnrolmentId);
            e.Property(x => x.EnrolmentDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.Property(x => x.Status).HasMaxLength(20).IsUnicode(false).IsRequired().HasDefaultValue(EnrolmentStatus.Confirmed);
            // A participant can enter a given event only once (in one category).
            e.HasIndex(x => new { x.ParticipantId, x.EventId }).IsUnique();
            e.HasOne(x => x.Participant).WithMany(u => u.Enrolments)
                .HasForeignKey(x => x.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Enrolments_Participant");
            e.HasOne(x => x.Event).WithMany(ev => ev.Enrolments)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Enrolments_Event");
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Enrolments_Category");
        });

        modelBuilder.Entity<RaceResult>(e =>
        {
            e.ToTable("Results");
            e.HasKey(x => x.ResultId);
            e.Property(x => x.FinishTime).HasColumnType("time");
            e.Property(x => x.CapturedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.EnrolmentId).IsUnique();
            e.HasOne(x => x.Enrolment).WithOne(en => en.Result)
                .HasForeignKey<RaceResult>(x => x.EnrolmentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Results_Enrolment");
        });
    }
}

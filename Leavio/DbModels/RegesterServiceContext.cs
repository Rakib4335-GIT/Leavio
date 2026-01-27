using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Leavio.DbModels;

public partial class RegesterServiceContext : DbContext
{
    public RegesterServiceContext()
    {
    }

    public RegesterServiceContext(DbContextOptions<RegesterServiceContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AdminInfo> AdminInfos { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<UserRole> UserRoles { get; set; }
    public virtual DbSet<SystemSettings> SystemSettings { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=192.168.10.202;Database=Regester_Service;User Id=sa;Password=123456;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminInfo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AdminInf__3214EC0780892F8E");

            entity.ToTable("AdminInfo");

            entity.Property(e => e.UserId).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Password).HasMaxLength(255);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Role__3214EC07");

            entity.ToTable("Role");

            entity.Property(e => e.RoleName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasMaxLength(500);
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserRole__3214EC07");

            entity.ToTable("User_Role");

            entity.Property(e => e.UserId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.RoleId).IsRequired();

            // Configure foreign key relationship to Role
            entity.HasOne(d => d.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_UserRole_Role");

            // Create unique index to prevent duplicate user-role assignments
            entity.HasIndex(e => new { e.UserId, e.RoleId })
                .IsUnique()
                .HasDatabaseName("IX_UserRole_UserId_RoleId");
        });

        modelBuilder.Entity<SystemSettings>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__SystemSettings__3214EC07");

            entity.ToTable("SystemSettings");

            entity.Property(e => e.SettingKey)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.SettingValue)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            // Create unique index on SettingKey
            entity.HasIndex(e => e.SettingKey)
                .IsUnique()
                .HasDatabaseName("IX_SystemSettings_SettingKey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

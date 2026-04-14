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
    public virtual DbSet<DailyLoginTracking> DailyLoginTrackings { get; set; }
        public virtual DbSet<MenuItem> MenuItems { get; set; }
        public virtual DbSet<LeaveType> LeaveTypes { get; set; }
        public virtual DbSet<LeaveBalance> LeaveBalances { get; set; }
        public virtual DbSet<LeaveApplication> LeaveApplications { get; set; }
        public virtual DbSet<EmploymentTypeDefinition> EmploymentTypes { get; set; }
        public virtual DbSet<LeaveTypeEmploymentAllocation> LeaveTypeEmploymentAllocations { get; set; }

        public virtual DbSet<RoleMenuPermission> RoleMenuPermissions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=192.168.10.202;Database=Leavio;User Id=sa;Password=123456;TrustServerCertificate=True;");

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
            
            // Optional fields - may not exist in database yet
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.ProfilePicture).HasMaxLength(500);

            entity.Property(e => e.EmploymentTypeId).IsRequired();

            entity.HasOne(e => e.EmploymentType)
                .WithMany(t => t.Employees)
                .HasForeignKey(e => e.EmploymentTypeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_AdminInfo_EmploymentType");
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

        modelBuilder.Entity<DailyLoginTracking>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DailyLoginTracking__3214EC07");

            entity.ToTable("DailyLoginTracking");

            entity.Property(e => e.EmployeeId)
                .IsRequired();

            entity.Property(e => e.FirstLoginTime)
                .IsRequired();

            entity.Property(e => e.TrackingDate)
                .IsRequired();

            // Configure foreign key relationship to AdminInfo
            entity.HasOne(d => d.Employee)
                .WithMany()
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_DailyLoginTracking_AdminInfo");

            // Create index on EmployeeId and TrackingDate for efficient queries
            entity.HasIndex(e => new { e.EmployeeId, e.TrackingDate })
                .HasDatabaseName("IX_DailyLoginTracking_EmployeeId_TrackingDate");
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__MenuItem__3214EC07");

            entity.ToTable("MenuItem");

            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Url)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.Icon)
                .HasMaxLength(500);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.DisplayOrder)
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(e => e.CreatedDate)
                .IsRequired();

            entity.Property(e => e.ParentId)
                .IsRequired(false);

            entity.Property(e => e.RequiresAuthentication)
                .IsRequired()
                .HasDefaultValue(false);

            // Configure self-referencing relationship for parent-child menu items
            entity.HasOne(d => d.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(d => d.ParentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_MenuItem_Parent");

            // Create index on Status and DisplayOrder for efficient queries
            entity.HasIndex(e => new { e.Status, e.DisplayOrder })
                .HasDatabaseName("IX_MenuItem_Status_DisplayOrder");

            // Create index on ParentId for efficient queries
            entity.HasIndex(e => e.ParentId)
                .HasDatabaseName("IX_MenuItem_ParentId");
        });

        modelBuilder.Entity<RoleMenuPermission>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("RoleMenuPermission");

            entity.HasIndex(e => new { e.RoleId, e.MenuItemId })
                .IsUnique();

            entity.HasOne(d => d.Role)
                .WithMany()
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.MenuItem)
                .WithMany()
                .HasForeignKey(d => d.MenuItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeaveType>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("LeaveType");

            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.DefaultAllocationDays)
                .HasColumnType("decimal(5,2)")
                .HasDefaultValue(0);

            entity.Property(e => e.RequiresDocumentForMultiDay)
                .HasDefaultValue(false);

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.Property(e => e.DisplayOrder)
                .HasDefaultValue(0);

            entity.HasIndex(e => e.Name)
                .IsUnique()
                .HasDatabaseName("IX_LeaveType_Name");
        });

        modelBuilder.Entity<EmploymentTypeDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("EmploymentType");

            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.DisplayOrder)
                .HasDefaultValue(0);

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.HasIndex(e => e.Name)
                .IsUnique()
                .HasDatabaseName("IX_EmploymentType_Name");

            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("IX_EmploymentType_Code");
        });

        modelBuilder.Entity<LeaveTypeEmploymentAllocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("LeaveTypeEmploymentAllocation");

            entity.Property(e => e.AllocationDays)
                .HasColumnType("decimal(5,2)")
                .HasDefaultValue(0);

            entity.Property(e => e.IsAvailable)
                .HasDefaultValue(true);

            entity.HasOne(e => e.LeaveType)
                .WithMany(t => t.EmploymentAllocations)
                .HasForeignKey(e => e.LeaveTypeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_LeaveTypeEmploymentAllocation_LeaveType");

            entity.HasOne(e => e.EmploymentType)
                .WithMany(t => t.LeaveAllocations)
                .HasForeignKey(e => e.EmploymentTypeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_LeaveTypeEmploymentAllocation_EmploymentType");

            entity.HasIndex(e => new { e.LeaveTypeId, e.EmploymentTypeId })
                .IsUnique()
                .HasDatabaseName("UQ_LeaveType_EmploymentType");
        });

        modelBuilder.Entity<LeaveBalance>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("LeaveBalance");

            entity.Property(e => e.TotalAllocatedDays)
                .HasColumnType("decimal(5,2)")
                .IsRequired();

            entity.Property(e => e.UsedDays)
                .HasColumnType("decimal(5,2)")
                .HasDefaultValue(0);

            entity.Property(e => e.ValidFrom)
                .IsRequired();

            entity.Property(e => e.ValidTo)
                .IsRequired();

            entity.Property(e => e.UpdatedOn)
                .IsRequired();

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_LeaveBalance_AdminInfo");

            entity.HasOne(e => e.LeaveType)
                .WithMany(t => t.LeaveBalances)
                .HasForeignKey(e => e.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_LeaveBalance_LeaveType");

            entity.HasIndex(e => new { e.EmployeeId, e.LeaveTypeId, e.ValidFrom, e.ValidTo })
                .IsUnique()
                .HasDatabaseName("IX_LeaveBalance_Employee_LeaveType_Period");
        });

        modelBuilder.Entity<LeaveApplication>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("LeaveApplication");

            entity.Property(e => e.StartDate).IsRequired();
            entity.Property(e => e.EndDate).IsRequired();
            entity.Property(e => e.DurationDays)
                .HasColumnType("decimal(5,2)")
                .IsRequired();

            entity.Property(e => e.Reason)
                .HasMaxLength(2000)
                .IsRequired();

            entity.Property(e => e.SupportingDocumentPath)
                .HasMaxLength(500);

            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.AppliedOn)
                .IsRequired();

            entity.Property(e => e.ReviewerComments)
                .HasMaxLength(1000);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_LeaveApplication_Employee");

            entity.HasOne(e => e.LeaveType)
                .WithMany(t => t.LeaveApplications)
                .HasForeignKey(e => e.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_LeaveApplication_LeaveType");

            entity.HasOne(e => e.ReviewedByEmployee)
                .WithMany()
                .HasForeignKey(e => e.ReviewedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_LeaveApplication_ReviewedBy");

            entity.HasIndex(e => new { e.EmployeeId, e.Status })
                .HasDatabaseName("IX_LeaveApplication_Employee_Status");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

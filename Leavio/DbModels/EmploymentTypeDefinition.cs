namespace Leavio.DbModels;

/// <summary>Configurable employment category (e.g. Full-Time, Intern, Contractor).</summary>
public partial class EmploymentTypeDefinition
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Stable key for migration and defaults (e.g. FullTime, Intern).</summary>
    public string Code { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<LeaveTypeEmploymentAllocation> LeaveAllocations { get; set; } = new List<LeaveTypeEmploymentAllocation>();

    public virtual ICollection<AdminInfo> Employees { get; set; } = new List<AdminInfo>();
}

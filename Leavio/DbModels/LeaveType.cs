namespace Leavio.DbModels;

public partial class LeaveType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Fallback default when creating allocation rows for new employment categories.</summary>
    public decimal DefaultAllocationDays { get; set; }

    public bool RequiresDocumentForMultiDay { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public virtual ICollection<LeaveTypeEmploymentAllocation> EmploymentAllocations { get; set; } = new List<LeaveTypeEmploymentAllocation>();

    public virtual ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();

    public virtual ICollection<LeaveApplication> LeaveApplications { get; set; } = new List<LeaveApplication>();
}

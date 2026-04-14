namespace Leavio.DbModels;

/// <summary>Per leave type: days and availability for one employment category.</summary>
public partial class LeaveTypeEmploymentAllocation
{
    public int Id { get; set; }

    public int LeaveTypeId { get; set; }

    public int EmploymentTypeId { get; set; }

    public decimal AllocationDays { get; set; }

    public bool IsAvailable { get; set; } = true;

    public virtual LeaveType LeaveType { get; set; } = null!;

    public virtual EmploymentTypeDefinition EmploymentType { get; set; } = null!;
}

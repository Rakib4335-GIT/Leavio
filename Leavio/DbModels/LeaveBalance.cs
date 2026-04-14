namespace Leavio.DbModels;

public partial class LeaveBalance
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public int LeaveTypeId { get; set; }

    public decimal TotalAllocatedDays { get; set; }

    public decimal UsedDays { get; set; }

    public DateTime ValidFrom { get; set; }

    public DateTime ValidTo { get; set; }

    public DateTime UpdatedOn { get; set; } = DateTime.Now;

    public virtual AdminInfo Employee { get; set; } = null!;

    public virtual LeaveType LeaveType { get; set; } = null!;
}

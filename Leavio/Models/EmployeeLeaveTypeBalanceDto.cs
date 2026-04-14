namespace Leavio.Models;

public class EmployeeLeaveTypeBalanceDto
{
    public int LeaveTypeId { get; set; }

    public string LeaveTypeName { get; set; } = string.Empty;

    public decimal AllocatedDays { get; set; }

    public decimal UsedDays { get; set; }

    public decimal RemainingDays { get; set; }
}

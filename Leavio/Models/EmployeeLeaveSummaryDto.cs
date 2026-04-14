namespace Leavio.Models;

/// <summary>Aggregated leave figures for one employee for the current leave period (calendar year).</summary>
public class EmployeeLeaveSummaryDto
{
    public int EmployeeId { get; set; }

    /// <summary>Per leave type (only types allocated for this employee’s employment).</summary>
    public List<EmployeeLeaveTypeBalanceDto> LeaveTypes { get; set; } = new();

    /// <summary>Sum of allocated days across applicable leave types.</summary>
    public decimal TotalAllocatedDays { get; set; }

    /// <summary>Sum of (allocated − used) across applicable leave types.</summary>
    public decimal TotalRemainingDays { get; set; }

    /// <summary>Allocated days for Sick Leave (if applicable).</summary>
    public decimal SickAllocatedDays { get; set; }

    /// <summary>Remaining days for Sick Leave (if applicable).</summary>
    public decimal SickRemainingDays { get; set; }

    public DateTime ValidFrom { get; set; }

    public DateTime ValidTo { get; set; }
}

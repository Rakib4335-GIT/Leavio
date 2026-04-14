namespace Leavio.DbModels;

public partial class LeaveApplication
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public int LeaveTypeId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public decimal DurationDays { get; set; }

    public string Reason { get; set; } = null!;

    public string? SupportingDocumentPath { get; set; }

    public string Status { get; set; } = "Pending";

    public DateTime AppliedOn { get; set; } = DateTime.Now;

    public int? ReviewedByEmployeeId { get; set; }

    public DateTime? ReviewedOn { get; set; }

    public string? ReviewerComments { get; set; }

    public virtual AdminInfo Employee { get; set; } = null!;

    public virtual LeaveType LeaveType { get; set; } = null!;

    public virtual AdminInfo? ReviewedByEmployee { get; set; }
}

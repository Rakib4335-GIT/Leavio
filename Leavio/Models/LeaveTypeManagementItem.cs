namespace Leavio.Models;

public class LeaveEmploymentAllocationItem
{
    public int EmploymentTypeId { get; set; }

    public decimal AllocationDays { get; set; }

    public bool IsAvailable { get; set; } = true;
}

/// <summary>Payload for saving leave type rules from the Leave Management admin page.</summary>
public class LeaveTypeManagementItem
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<LeaveEmploymentAllocationItem> EmploymentAllocations { get; set; } = new();

    public bool RequiresDocumentForMultiDay { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

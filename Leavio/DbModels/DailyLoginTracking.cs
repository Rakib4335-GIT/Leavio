using System;

namespace Leavio.DbModels;

public partial class DailyLoginTracking
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public DateTime FirstLoginTime { get; set; }

    public DateTime? LastLogoutTime { get; set; }

    public DateTime TrackingDate { get; set; }

    // Navigation property to AdminInfo
    public virtual AdminInfo? Employee { get; set; }
}

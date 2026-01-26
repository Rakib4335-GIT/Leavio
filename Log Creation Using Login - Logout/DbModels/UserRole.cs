using System;
using System.Collections.Generic;

namespace OfficeFlow.DbModels;

public partial class UserRole
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public int RoleId { get; set; }

    // Navigation properties
    public virtual Role Role { get; set; } = null!;
}

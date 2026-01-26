using System;
using System.Collections.Generic;

namespace Log_Creation_Using_Login___Logout.DbModels;

public partial class UserRole
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int RoleId { get; set; }

    // Navigation properties
    public virtual AdminInfo User { get; set; } = null!;
    public virtual Role Role { get; set; } = null!;
}

using System;
using System.Collections.Generic;

namespace Log_Creation_Using_Login___Logout.DbModels;

public partial class AdminInfo
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public int RoleId { get; set; }

    // Navigation property
    public virtual Role Role { get; set; } = null!;
}

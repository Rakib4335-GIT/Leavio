using System;
using System.Collections.Generic;

namespace Log_Creation_Using_Login___Logout.DbModels;

public partial class Role
{
    public int Id { get; set; }

    public string RoleName { get; set; } = null!;

    public string? Description { get; set; }

    // Navigation property to User_Role (many-to-many through UserRole)
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

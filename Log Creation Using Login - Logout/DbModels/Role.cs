using System;
using System.Collections.Generic;

namespace Log_Creation_Using_Login___Logout.DbModels;

public partial class Role
{
    public int Id { get; set; }

    public string RoleName { get; set; } = null!;

    public string? Description { get; set; }

    // Navigation property
    public virtual ICollection<AdminInfo> AdminInfos { get; set; } = new List<AdminInfo>();
}

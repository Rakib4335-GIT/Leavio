using System;
using System.Collections.Generic;

namespace Leavio.DbModels;

public partial class RoleMenuPermission
{
    public int Id { get; set; }

    public int RoleId { get; set; }

    public int MenuItemId { get; set; }

    public virtual Role? Role { get; set; }

    public virtual MenuItem? MenuItem { get; set; }
}


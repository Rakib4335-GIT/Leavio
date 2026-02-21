using System;
using System.Collections.Generic;

namespace Leavio.DbModels;

public partial class MenuItem
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Url { get; set; } = null!;

    public bool Status { get; set; } = true;

    public string? Icon { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public int? ParentId { get; set; }

    public bool RequiresAuthentication { get; set; } = false;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public DateTime? UpdatedDate { get; set; }

    // Navigation properties for parent-child relationship
    public virtual MenuItem? Parent { get; set; }

    public virtual ICollection<MenuItem> Children { get; set; } = new List<MenuItem>();
}

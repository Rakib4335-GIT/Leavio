using System;
using System.Collections.Generic;

namespace Leavio.DbModels;

public partial class AdminInfo
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? ProfilePicture { get; set; }

    public int EmploymentTypeId { get; set; }

    public virtual EmploymentTypeDefinition EmploymentType { get; set; } = null!;
}

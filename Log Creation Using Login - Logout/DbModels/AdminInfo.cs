using System;
using System.Collections.Generic;

namespace OfficeFlow.DbModels;

public partial class AdminInfo
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;
}

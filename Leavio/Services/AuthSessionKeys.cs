namespace Leavio.Services;

/// <summary>Keys stored in sessionStorage (tab) and optionally localStorage (Remember Me).</summary>
public static class AuthSessionKeys
{
    public const string AdminId = "adminId";
    public const string AdminName = "adminName";
    public const string AdminEmail = "adminEmail";
    public const string AdminRole = "adminRole";
    public const string EmployeeId = "employeeId";

    public static readonly string[] All = { AdminId, AdminName, AdminEmail, AdminRole, EmployeeId };
}

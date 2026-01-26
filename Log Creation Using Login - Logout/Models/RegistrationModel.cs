namespace Log_Creation_Using_Login___Logout.Models
{
    public class RegistrationModel
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public Role Role { get; set; } = Role.User;
    }
}

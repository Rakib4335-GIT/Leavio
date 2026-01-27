namespace Leavio.DbModels;

public partial class SystemSettings
{
    public int Id { get; set; }
    
    public string SettingKey { get; set; } = null!;
    
    public string SettingValue { get; set; } = null!;
    
    public string? Description { get; set; }
}

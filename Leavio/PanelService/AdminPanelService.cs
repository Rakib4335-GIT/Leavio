using System.Text.Json;
using Leavio.DbModels;
using Leavio.Models;
using Microsoft.EntityFrameworkCore;

namespace Leavio.PanelService
{
    public class AdminPanelService
    {
        private readonly IDbContextFactory<RegesterServiceContext> _contextFactory;
        private static List<DbModels.Role>? _cachedRoles = null;
        private static DateTime _rolesCacheTime = DateTime.MinValue;
        private static readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5); // Cache for 5 minutes
        private static readonly HashSet<string> StaticMenuUrls = new(StringComparer.OrdinalIgnoreCase)
        {
            "/",
            "/contact",
            "/aboutUs"
        };
        
        public AdminPanelService(IDbContextFactory<RegesterServiceContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }
        
        public async Task<ResponseModel> Register(RegistrationModel registrationModel)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                
                // Validate input
                if (string.IsNullOrWhiteSpace(registrationModel.UserId))
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "User ID is required."
                    };
                }

                if (string.IsNullOrWhiteSpace(registrationModel.Email))
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Email is required."
                    };
                }

                if (string.IsNullOrWhiteSpace(registrationModel.Password))
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Password is required."
                    };
                }

                if (registrationModel.Email.Length > 255)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Email address is too long (maximum 255 characters)."
                    };
                }

                // Validate UserId if provided
                if (!string.IsNullOrWhiteSpace(registrationModel.UserId))
                {
                    var existingUserId = await context.AdminInfos
                        .AsNoTracking()
                        .Where(x => x.UserId == registrationModel.UserId)
                        .Select(x => new { x.Id, x.UserId })
                        .FirstOrDefaultAsync();
                    if (existingUserId != null)
                    {
                        return new ResponseModel
                        {
                            Success = false,
                            Message = $"User ID '{registrationModel.UserId}' already exists. Please choose a different User ID."
                        };
                    }
                }

                var existingAdmin = await context.AdminInfos
                    .AsNoTracking()
                    .Where(x => x.Email == registrationModel.Email)
                    .Select(x => new { x.Id, x.Email })
                    .FirstOrDefaultAsync();
                if (existingAdmin != null)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "A user with this email already exists. Please use a different email address."
                    };
                }

                // Validate RoleId exists if provided
                if (registrationModel.RoleId.HasValue)
                {
                    var roleExists = await context.Roles
                        .AsNoTracking()
                        .AnyAsync(x => x.Id == registrationModel.RoleId.Value);
                    if (!roleExists)
                    {
                        return new ResponseModel
                        {
                            Success = false,
                            Message = "Invalid role selected."
                        };
                    }
                }
                
                await EnsureDefaultEmploymentTypesAsync(context);
                await EnsureDefaultLeaveTypesAsync(context);
                await EnsureLeaveAllocationMatrixForAllTypesAsync(context);

                var defaultEmpId = await context.EmploymentTypes
                    .Where(e => e.Code == "FullTime")
                    .Select(e => e.Id)
                    .FirstAsync();

                var dbAdminInfo = new AdminInfo
                {
                    UserId = registrationModel.UserId,
                    Name = registrationModel.Name,
                    Email = registrationModel.Email,
                    Password = registrationModel.Password,
                    PhoneNumber = null,
                    ProfilePicture = null,
                    EmploymentTypeId = defaultEmpId
                };

                await context.AdminInfos.AddAsync(dbAdminInfo);
                await context.SaveChangesAsync();

                // Determine which role to assign
                int roleIdToAssign;
                if (registrationModel.RoleId.HasValue)
                {
                    // Use the provided role
                    roleIdToAssign = registrationModel.RoleId.Value;
                }
                else
                {
                    // Default to "User" role
                    var defaultRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "User");
                    if (defaultRole == null)
                    {
                        return new ResponseModel
                        {
                            Success = false,
                            Message = "Default 'User' role not found. Please contact administrator."
                        };
                    }
                    roleIdToAssign = defaultRole.Id;
                }

                // Create User_Role entry (primary source for role assignment)
                if (!string.IsNullOrEmpty(registrationModel.UserId))
                {
                    var userRole = new UserRole
                    {
                        UserId = registrationModel.UserId,
                        RoleId = roleIdToAssign
                    };
                    await context.UserRoles.AddAsync(userRole);
                    await context.SaveChangesAsync();
                }

                return new ResponseModel
                {
                    Success = true,
                    Message = "Registration Successful."
                };
            }
            catch (DbUpdateException ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                
                // Check for duplicate key errors and provide user-friendly messages
                if (innerMessage.Contains("duplicate key") || innerMessage.Contains("IX_AdminInfo_Userld") || innerMessage.Contains("IX_AdminInfo_UserId"))
                {
                    // Extract UserId from error message if possible
                    var userIdMatch = System.Text.RegularExpressions.Regex.Match(innerMessage, @"\(([^)]+)\)");
                    if (userIdMatch.Success)
                    {
                        var duplicateUserId = userIdMatch.Groups[1].Value;
                        return new ResponseModel
                        {
                            Success = false,
                            Message = $"User ID '{duplicateUserId}' already exists. Please choose a different User ID."
                        };
                    }
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "This User ID already exists. Please choose a different User ID."
                    };
                }
                
                if (innerMessage.Contains("duplicate key") && innerMessage.Contains("Email"))
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "A user with this email already exists. Please use a different email address."
                    };
                }
                
                return new ResponseModel
                {
                    Success = false,
                    Message = $"An error occurred while saving. Please check your input and try again."
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }


        public async Task<ResponseModel> Login(LoginModel loginModel)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                if (string.IsNullOrWhiteSpace(loginModel.EmailId))
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "User Name or Email is required."
                    };
                }

                if (string.IsNullOrWhiteSpace(loginModel.Password))
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Password is required."
                    };
                }

                // Try to find user by Email or UserId (case-insensitive)
                var loginInput = loginModel.EmailId?.Trim();
                if (string.IsNullOrEmpty(loginInput))
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "User Name or Email is required."
                    };
                }
                
                // Use ToLower() for case-insensitive comparison (works with SQL Server and most databases)
                var loginInputLower = loginInput.ToLower();
                
                // Try to get user - handle optional columns gracefully
                AdminInfo? user = null;
                try
                {
                    // First try with all columns (if they exist)
                    user = await context.AdminInfos
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => 
                            (!string.IsNullOrEmpty(x.Email) && x.Email.ToLower() == loginInputLower) ||
                            (!string.IsNullOrEmpty(x.UserId) && x.UserId.ToLower() == loginInputLower));
                }
                catch
                {
                    // If optional columns don't exist, use explicit selection
                    user = await context.AdminInfos
                        .AsNoTracking()
                        .Where(x => 
                            (!string.IsNullOrEmpty(x.Email) && x.Email.ToLower() == loginInputLower) ||
                            (!string.IsNullOrEmpty(x.UserId) && x.UserId.ToLower() == loginInputLower))
                        .Select(x => new AdminInfo
                        {
                            Id = x.Id,
                            UserId = x.UserId,
                            Name = x.Name,
                            Email = x.Email,
                            Password = x.Password
                        })
                        .FirstOrDefaultAsync();
                }
                    
                if (user == null)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "User Name or Email Not Registered"
                    };
                }

                if (user.Password != loginModel.Password)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Your Password is Incorrect"
                    };
                }

                // Get role from User_Role table (primary source)
                var userRole = await context.UserRoles
                    .AsNoTracking()
                    .Include(ur => ur.Role)
                    .FirstOrDefaultAsync(ur => ur.UserId == user.UserId);

                // Get role name from User_Role table
                var roleName = userRole?.Role.RoleName ?? "User";

                // Track login time (only first login of the day)
                await TrackLoginAsync(user.Id);

                return new ResponseModel
                {
                    Success = true,
                    Message = $"Login Successful! User ID: {user.Id} | Name: {user.Name} | Email: {user.Email} | Role: {roleName} | EmployeeId: {user.Id}"
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }

        public async Task TrackLoginAsync(int employeeId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                
                if (employeeId <= 0)
                {
                    return;
                }

                var today = DateTime.Now.Date;
                var currentTime = DateTime.Now;

                // Check if there's already a record for today
                var existingRecord = await context.DailyLoginTrackings
                    .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.TrackingDate == today);

                if (existingRecord == null)
                {
                    // First login of the day - create new record
                    var newRecord = new DailyLoginTracking
                    {
                        EmployeeId = employeeId,
                        FirstLoginTime = currentTime,
                        TrackingDate = today,
                        LastLogoutTime = null
                    };
                    await context.DailyLoginTrackings.AddAsync(newRecord);
                    await context.SaveChangesAsync();
                }
                // If record exists, we don't update FirstLoginTime (it should remain the first login time)
            }
            catch (Exception)
            {
                // Silently fail - don't break login if tracking fails
            }
        }

        public async Task TrackLogoutAsync(int employeeId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                
                if (employeeId <= 0)
                {
                    return;
                }

                var today = DateTime.Now.Date;
                var currentTime = DateTime.Now;

                // Find today's record
                var existingRecord = await context.DailyLoginTrackings
                    .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.TrackingDate == today);

                if (existingRecord != null)
                {
                    // Update last logout time
                    existingRecord.LastLogoutTime = currentTime;
                    await context.SaveChangesAsync();
                }
                // If no record exists, we don't create one on logout (logout without login doesn't make sense)
            }
            catch (Exception)
            {
                // Silently fail - don't break logout if tracking fails
            }
        }

        public async Task<List<DailyLoginTracking>> GetDailyLoginTrackingAsync(int? employeeId = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                
                var query = context.DailyLoginTrackings
                    .Include(x => x.Employee)
                    .AsQueryable();

                // Filter by EmployeeId if provided
                if (employeeId.HasValue && employeeId.Value > 0)
                {
                    query = query.Where(x => x.EmployeeId == employeeId.Value);
                }

                // Filter by date range if provided
                if (startDate.HasValue)
                {
                    query = query.Where(x => x.TrackingDate >= startDate.Value.Date);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(x => x.TrackingDate <= endDate.Value.Date);
                }

                return await query
                    .OrderByDescending(x => x.TrackingDate)
                    .ThenByDescending(x => x.FirstLoginTime)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<DailyLoginTracking>();
            }
        }

        public async Task<List<DbModels.Role>> GetAllRolesAsync()
        {
            // Return cached roles if still valid
            if (_cachedRoles != null && DateTime.UtcNow - _rolesCacheTime < _cacheExpiry)
            {
                return _cachedRoles;
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var roles = await context.Roles
                    .AsNoTracking()
                    .OrderBy(r => r.RoleName)
                    .ToListAsync();
                
                // Update cache
                _cachedRoles = roles;
                _rolesCacheTime = DateTime.UtcNow;
                
                return roles;
            }
            catch (Exception)
            {
                // Return cached data if available, even if expired, as fallback
                return _cachedRoles ?? new List<DbModels.Role>();
            }
        }

        // Method to invalidate roles cache (call this when roles are modified)
        public static void InvalidateRolesCache()
        {
            _cachedRoles = null;
            _rolesCacheTime = DateTime.MinValue;
        }

        public async Task<List<UserRole>> GetAllUserRolesAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.UserRoles
                    .AsNoTracking()
                    .Include(ur => ur.Role)
                    .OrderBy(ur => ur.UserId)
                    .ThenBy(ur => ur.Role.RoleName)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<UserRole>();
            }
        }

        public async Task<ResponseModel> AssignRoleToUser(int userId, int roleId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Check if user exists and get their UserId (string)
                var user = await context.AdminInfos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null || string.IsNullOrEmpty(user.UserId))
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "User not found or UserId is not set."
                    };
                }

                // Check if role exists
                var roleExists = await context.Roles
                    .AsNoTracking()
                    .AnyAsync(r => r.Id == roleId);
                if (!roleExists)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Role not found."
                    };
                }

                // Check if assignment already exists
                var existingAssignment = await context.UserRoles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ur => ur.UserId == user.UserId && ur.RoleId == roleId);
                
                if (existingAssignment != null)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "User already has this role assigned."
                    };
                }

                var userRole = new UserRole
                {
                    UserId = user.UserId,
                    RoleId = roleId
                };

                await context.UserRoles.AddAsync(userRole);
                await context.SaveChangesAsync();

                return new ResponseModel
                {
                    Success = true,
                    Message = "Role assigned successfully."
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }

        public async Task<ResponseModel> RemoveRoleFromUser(int userRoleId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var userRole = await context.UserRoles.FindAsync(userRoleId);
                if (userRole == null)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "User role assignment not found."
                    };
                }

                context.UserRoles.Remove(userRole);
                await context.SaveChangesAsync();

                return new ResponseModel
                {
                    Success = true,
                    Message = "Role removed successfully."
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }

        public async Task<List<AdminInfo>> GetAllUsersAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                // Use raw SQL or handle optional columns gracefully
                return await context.AdminInfos
                    .AsNoTracking()
                    .OrderBy(u => u.Name)
                    .Select(u => new AdminInfo
                    {
                        Id = u.Id,
                        UserId = u.UserId,
                        Name = u.Name,
                        Email = u.Email,
                        Password = u.Password,
                        PhoneNumber = u.PhoneNumber,
                        ProfilePicture = u.ProfilePicture,
                        EmploymentTypeId = u.EmploymentTypeId
                    })
                    .ToListAsync();
            }
            catch (Exception)
            {
                // If columns don't exist, try without optional fields
                try
                {
                    using var context = await _contextFactory.CreateDbContextAsync();
                    return await context.AdminInfos
                        .AsNoTracking()
                        .OrderBy(u => u.Name)
                        .Select(u => new AdminInfo
                        {
                            Id = u.Id,
                            UserId = u.UserId,
                            Name = u.Name,
                            Email = u.Email,
                            Password = u.Password
                        })
                        .ToListAsync();
                }
                catch
                {
                    return new List<AdminInfo>();
                }
            }
        }

        public async Task<ResponseModel> UpdateUser(string userId, string name, string email, string? password = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Find user by UserId
                var user = await context.AdminInfos.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "User not found."
                    };
                }

                // Check if email is being changed and if it already exists
                if (user.Email != email)
                {
                    var emailExists = await context.AdminInfos
                        .AsNoTracking()
                        .AnyAsync(u => u.Email == email && u.UserId != userId);
                    if (emailExists)
                    {
                        return new ResponseModel
                        {
                            Success = false,
                            Message = "Email already exists. Please use a different email."
                        };
                    }
                }

                // Validate email length
                if (email.Length > 255)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Email address is too long (maximum 255 characters)."
                    };
                }

                // Update user information
                user.Name = name;
                user.Email = email;
                
                // Only update password if provided
                if (!string.IsNullOrWhiteSpace(password))
                {
                    user.Password = password;
                }

                await context.SaveChangesAsync();

                return new ResponseModel
                {
                    Success = true,
                    Message = "User updated successfully."
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }

        public async Task<bool> IsRegistrationEnabledAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var setting = await context.SystemSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SettingKey == "RegistrationEnabled");
                
                if (setting == null)
                {
                    // Default to enabled if setting doesn't exist
                    return true;
                }
                
                return bool.TryParse(setting.SettingValue, out var result) && result;
            }
            catch (Exception)
            {
                // Default to enabled on error
                return true;
            }
        }

        public async Task<ResponseModel> SetRegistrationEnabledAsync(bool enabled)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                
                var setting = await context.SystemSettings
                    .FirstOrDefaultAsync(s => s.SettingKey == "RegistrationEnabled");
                
                if (setting == null)
                {
                    // Create new setting
                    setting = new SystemSettings
                    {
                        SettingKey = "RegistrationEnabled",
                        SettingValue = enabled.ToString(),
                        Description = "Controls whether user registration is enabled or disabled"
                    };
                    await context.SystemSettings.AddAsync(setting);
                }
                else
                {
                    // Update existing setting
                    setting.SettingValue = enabled.ToString();
                }
                
                await context.SaveChangesAsync();
                
                return new ResponseModel
                {
                    Success = true,
                    Message = $"Registration has been {(enabled ? "enabled" : "disabled")}."
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Gets the menu layout: "Top" (horizontal header) or "Sidebar" (vertical left sidebar).
        /// Default is "Top" if not set.
        /// </summary>
        public async Task<string> GetMenuLayoutAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var setting = await context.SystemSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SettingKey == "MenuLayout");
                if (setting == null || string.IsNullOrWhiteSpace(setting.SettingValue))
                    return "Top";
                var value = setting.SettingValue.Trim();
                return value.Equals("Sidebar", StringComparison.OrdinalIgnoreCase) ? "Sidebar" : "Top";
            }
            catch (Exception)
            {
                return "Top";
            }
        }

        /// <summary>
        /// Sets the menu layout to "Top" or "Sidebar".
        /// </summary>
        public async Task<ResponseModel> SetMenuLayoutAsync(string layout)
        {
            var normalized = layout?.Trim().Equals("Sidebar", StringComparison.OrdinalIgnoreCase) == true ? "Sidebar" : "Top";
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var setting = await context.SystemSettings
                    .FirstOrDefaultAsync(s => s.SettingKey == "MenuLayout");
                if (setting == null)
                {
                    setting = new SystemSettings
                    {
                        SettingKey = "MenuLayout",
                        SettingValue = normalized,
                        Description = "Menu position: Top (horizontal header) or Sidebar (vertical left)"
                    };
                    await context.SystemSettings.AddAsync(setting);
                }
                else
                {
                    setting.SettingValue = normalized;
                }
                await context.SaveChangesAsync();
                return new ResponseModel
                {
                    Success = true,
                    Message = $"Menu is now shown in the {normalized.ToLowerInvariant()}."
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }

        /// <summary>Gets the home page title. Default: "Hello, world!"</summary>
        public async Task<string> GetHomePageTitleAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var setting = await context.SystemSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SettingKey == "HomePageTitle");
                return string.IsNullOrWhiteSpace(setting?.SettingValue) ? "Hello, world!" : setting.SettingValue.Trim();
            }
            catch (Exception)
            {
                return "Hello, world!";
            }
        }

        /// <summary>Gets the home page subtitle/welcome text. Default: "Welcome to your new app."</summary>
        public async Task<string> GetHomePageSubtitleAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var setting = await context.SystemSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SettingKey == "HomePageSubtitle");
                return string.IsNullOrWhiteSpace(setting?.SettingValue) ? "Welcome to your new app." : setting.SettingValue.Trim();
            }
            catch (Exception)
            {
                return "Welcome to your new app.";
            }
        }

        /// <summary>Saves the home page title and subtitle.</summary>
        public async Task<ResponseModel> SetHomePageContentAsync(string title, string subtitle)
        {
            if (string.IsNullOrWhiteSpace(title))
                title = "Hello, world!";
            if (string.IsNullOrWhiteSpace(subtitle))
                subtitle = "Welcome to your new app.";
            title = title.Trim();
            subtitle = subtitle.Trim();
            if (title.Length > 200)
                return new ResponseModel { Success = false, Message = "Title cannot exceed 200 characters." };
            if (subtitle.Length > 500)
                return new ResponseModel { Success = false, Message = "Subtitle cannot exceed 500 characters." };
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                foreach (var key in new[] { "HomePageTitle", "HomePageSubtitle" })
                {
                    var setting = await context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
                    var value = key == "HomePageTitle" ? title : subtitle;
                    if (setting == null)
                    {
                        setting = new SystemSettings
                        {
                            SettingKey = key,
                            SettingValue = value,
                            Description = key == "HomePageTitle" ? "Home page main heading" : "Home page welcome text"
                        };
                        await context.SystemSettings.AddAsync(setting);
                    }
                    else
                    {
                        setting.SettingValue = value;
                    }
                }
                await context.SaveChangesAsync();
                return new ResponseModel { Success = true, Message = "Home page content saved successfully." };
            }
            catch (Exception ex)
            {
                return new ResponseModel { Success = false, Message = $"An error occurred: {ex.Message}" };
            }
        }

        private static readonly string ContactPrefix = "Contact_";

        /// <summary>Gets all contact page content from settings. Missing keys return model defaults.</summary>
        public async Task<ContactPageContent> GetContactPageContentAsync()
        {
            var model = new ContactPageContent();
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var settings = await context.SystemSettings
                    .AsNoTracking()
                    .Where(s => s.SettingKey.StartsWith(ContactPrefix))
                    .ToListAsync();
                var dict = settings.ToDictionary(s => s.SettingKey, s => s.SettingValue ?? "");
                foreach (var prop in typeof(ContactPageContent).GetProperties())
                {
                    if (prop.CanRead && prop.CanWrite && prop.PropertyType == typeof(string))
                    {
                        var key = ContactPrefix + prop.Name;
                        if (dict.TryGetValue(key, out var val) && !string.IsNullOrEmpty(val))
                            prop.SetValue(model, val.Trim());
                    }
                }
                if (dict.TryGetValue(ContactPrefix + "SocialLinks", out var socialLinksJson) && !string.IsNullOrWhiteSpace(socialLinksJson))
                {
                    try
                    {
                        var list = JsonSerializer.Deserialize<List<SocialLinkItem>>(socialLinksJson);
                        if (list != null)
                            model.SocialLinks = list;
                    }
                    catch { /* keep default */ }
                }
                else if (dict.TryGetValue(ContactPrefix + "SocialUrls", out var socialUrlsJson) && !string.IsNullOrWhiteSpace(socialUrlsJson))
                {
                    try
                    {
                        var urlList = JsonSerializer.Deserialize<List<string>>(socialUrlsJson);
                        if (urlList != null && urlList.Count > 0)
                            model.SocialLinks = urlList.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => new SocialLinkItem { Url = u.Trim(), Name = "", Icon = "" }).ToList();
                    }
                    catch { /* keep default */ }
                }
                else
                {
                    var legacy = new List<SocialLinkItem>();
                    for (int i = 1; i <= 5; i++)
                    {
                        if (dict.TryGetValue(ContactPrefix + "Social" + i + "Url", out var u) && !string.IsNullOrWhiteSpace(u))
                            legacy.Add(new SocialLinkItem { Url = u.Trim(), Name = "", Icon = "" });
                    }
                    if (legacy.Count > 0)
                        model.SocialLinks = legacy;
                }
                return model;
            }
            catch (Exception)
            {
                return model;
            }
        }

        /// <summary>Saves all contact page content to settings.</summary>
        public async Task<ResponseModel> SetContactPageContentFullAsync(ContactPageContent content)
        {
            if (content == null)
                return new ResponseModel { Success = false, Message = "Content is required." };
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                foreach (var prop in typeof(ContactPageContent).GetProperties())
                {
                    if (prop.CanRead && prop.CanWrite && prop.PropertyType == typeof(string))
                    {
                        var key = ContactPrefix + prop.Name;
                        var value = (prop.GetValue(content) as string)?.Trim() ?? "";
                        var setting = await context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
                        if (setting == null)
                        {
                            setting = new SystemSettings
                            {
                                SettingKey = key,
                                SettingValue = value,
                                Description = "Contact page: " + prop.Name
                            };
                            await context.SystemSettings.AddAsync(setting);
                        }
                        else
                        {
                            setting.SettingValue = value;
                        }
                    }
                }
                var socialLinksJson = JsonSerializer.Serialize(content.SocialLinks ?? new List<SocialLinkItem>());
                var socialKey = ContactPrefix + "SocialLinks";
                var socialSetting = await context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == socialKey);
                if (socialSetting == null)
                {
                    socialSetting = new SystemSettings { SettingKey = socialKey, SettingValue = socialLinksJson, Description = "Contact page: SocialLinks (JSON)" };
                    await context.SystemSettings.AddAsync(socialSetting);
                }
                else
                    socialSetting.SettingValue = socialLinksJson;
                var oldSocialUrls = await context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == ContactPrefix + "SocialUrls");
                if (oldSocialUrls != null) context.SystemSettings.Remove(oldSocialUrls);
                for (int i = 1; i <= 10; i++)
                {
                    var oldKey = ContactPrefix + "Social" + i + "Url";
                    var old = await context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == oldKey);
                    if (old != null) context.SystemSettings.Remove(old);
                }
                await context.SaveChangesAsync();
                return new ResponseModel { Success = true, Message = "Contact page content saved successfully." };
            }
            catch (Exception ex)
            {
                return new ResponseModel { Success = false, Message = $"An error occurred: {ex.Message}" };
            }
        }

        private static readonly string AboutUsPrefix = "AboutUs_";

        /// <summary>Gets all About Us page content from settings. Missing keys return model defaults.</summary>
        public async Task<AboutUsPageContent> GetAboutUsPageContentAsync()
        {
            var model = new AboutUsPageContent();
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var settings = await context.SystemSettings
                    .AsNoTracking()
                    .Where(s => s.SettingKey.StartsWith(AboutUsPrefix))
                    .ToListAsync();
                var dict = settings.ToDictionary(s => s.SettingKey, s => s.SettingValue ?? "");
                foreach (var prop in typeof(AboutUsPageContent).GetProperties())
                {
                    if (prop.CanRead && prop.CanWrite && prop.PropertyType == typeof(string))
                    {
                        var key = AboutUsPrefix + prop.Name;
                        if (dict.TryGetValue(key, out var val) && !string.IsNullOrEmpty(val))
                            prop.SetValue(model, val.Trim());
                    }
                }
                if (dict.TryGetValue(AboutUsPrefix + "Values", out var valuesJson) && !string.IsNullOrWhiteSpace(valuesJson))
                {
                    try
                    {
                        var list = JsonSerializer.Deserialize<List<ValueCardItem>>(valuesJson);
                        if (list != null && list.Count > 0)
                            model.Values = list;
                    }
                    catch { /* keep default Values */ }
                }
                else
                {
                    var legacy = new List<ValueCardItem>();
                    for (int i = 1; i <= 3; i++)
                    {
                        if (dict.TryGetValue(AboutUsPrefix + "Value" + i + "Title", out var t) && dict.TryGetValue(AboutUsPrefix + "Value" + i + "Text", out var b))
                            legacy.Add(new ValueCardItem { Title = (t ?? "").Trim(), Text = (b ?? "").Trim() });
                    }
                    if (legacy.Count > 0)
                        model.Values = legacy;
                }
                return model;
            }
            catch (Exception)
            {
                return model;
            }
        }

        /// <summary>Saves all About Us page content to settings.</summary>
        public async Task<ResponseModel> SetAboutUsPageContentFullAsync(AboutUsPageContent content)
        {
            if (content == null)
                return new ResponseModel { Success = false, Message = "Content is required." };
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                foreach (var prop in typeof(AboutUsPageContent).GetProperties())
                {
                    if (prop.CanRead && prop.CanWrite && prop.PropertyType == typeof(string))
                    {
                        var key = AboutUsPrefix + prop.Name;
                        var value = (prop.GetValue(content) as string)?.Trim() ?? "";
                        var setting = await context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
                        if (setting == null)
                        {
                            setting = new SystemSettings
                            {
                                SettingKey = key,
                                SettingValue = value,
                                Description = "About Us page: " + prop.Name
                            };
                            await context.SystemSettings.AddAsync(setting);
                        }
                        else
                        {
                            setting.SettingValue = value;
                        }
                    }
                }
                var valuesJson = JsonSerializer.Serialize(content.Values ?? new List<ValueCardItem>());
                var valuesKey = AboutUsPrefix + "Values";
                var valuesSetting = await context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == valuesKey);
                if (valuesSetting == null)
                {
                    valuesSetting = new SystemSettings { SettingKey = valuesKey, SettingValue = valuesJson, Description = "About Us page: Values (JSON)" };
                    await context.SystemSettings.AddAsync(valuesSetting);
                }
                else
                    valuesSetting.SettingValue = valuesJson;
                for (int i = 1; i <= 10; i++)
                {
                    var k1 = AboutUsPrefix + "Value" + i + "Title";
                    var k2 = AboutUsPrefix + "Value" + i + "Text";
                    var old1 = await context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == k1);
                    var old2 = await context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == k2);
                    if (old1 != null) context.SystemSettings.Remove(old1);
                    if (old2 != null) context.SystemSettings.Remove(old2);
                }
                await context.SaveChangesAsync();
                return new ResponseModel { Success = true, Message = "About Us page content saved successfully." };
            }
            catch (Exception ex)
            {
                return new ResponseModel { Success = false, Message = $"An error occurred: {ex.Message}" };
            }
        }

        private static readonly string HomePrefix = "Home_";

        /// <summary>Gets all homepage content from settings. Missing keys return model defaults.</summary>
        public async Task<HomePageContent> GetHomePageContentAsync()
        {
            var model = new HomePageContent();
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var settings = await context.SystemSettings
                    .AsNoTracking()
                    .Where(s => s.SettingKey.StartsWith(HomePrefix) || s.SettingKey == "HomePageTitle" || s.SettingKey == "HomePageSubtitle")
                    .ToListAsync();
                var dict = settings.ToDictionary(s => s.SettingKey, s => s.SettingValue ?? "");
                foreach (var prop in typeof(HomePageContent).GetProperties())
                {
                    if (prop.CanRead && prop.CanWrite && prop.PropertyType == typeof(string))
                    {
                        var key = HomePrefix + prop.Name;
                        if (dict.TryGetValue(key, out var val) && !string.IsNullOrEmpty(val))
                            prop.SetValue(model, val.Trim());
                    }
                }
                if (!dict.ContainsKey(HomePrefix + "HeroTitle") && dict.TryGetValue("HomePageTitle", out var oldTitle) && !string.IsNullOrWhiteSpace(oldTitle))
                    model.HeroTitle = oldTitle.Trim();
                if (!dict.ContainsKey(HomePrefix + "HeroSubtitle") && dict.TryGetValue("HomePageSubtitle", out var oldSub) && !string.IsNullOrWhiteSpace(oldSub))
                    model.HeroSubtitle = oldSub.Trim();
                return model;
            }
            catch (Exception)
            {
                return model;
            }
        }

        /// <summary>Saves all homepage content to settings.</summary>
        public async Task<ResponseModel> SetHomePageContentFullAsync(HomePageContent content)
        {
            if (content == null)
                return new ResponseModel { Success = false, Message = "Content is required." };
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                foreach (var prop in typeof(HomePageContent).GetProperties())
                {
                    if (prop.CanRead && prop.CanWrite && prop.PropertyType == typeof(string))
                    {
                        var key = HomePrefix + prop.Name;
                        var value = (prop.GetValue(content) as string)?.Trim() ?? "";
                        var setting = await context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
                        if (setting == null)
                        {
                            setting = new SystemSettings
                            {
                                SettingKey = key,
                                SettingValue = value,
                                Description = "Home page: " + prop.Name
                            };
                            await context.SystemSettings.AddAsync(setting);
                        }
                        else
                        {
                            setting.SettingValue = value;
                        }
                    }
                }
                await context.SaveChangesAsync();
                return new ResponseModel { Success = true, Message = "Home page content saved successfully." };
            }
            catch (Exception ex)
            {
                return new ResponseModel { Success = false, Message = $"An error occurred: {ex.Message}" };
            }
        }

        public async Task<ResponseModel> UpdateProfileAsync(string userId, string name, string email, string? phoneNumber = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Find user by UserId
                var user = await context.AdminInfos.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "User not found."
                    };
                }

                // Validate name
                if (string.IsNullOrWhiteSpace(name))
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Name cannot be empty."
                    };
                }

                if (name.Length > 100)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Name is too long (maximum 100 characters)."
                    };
                }

                // Update user information
                user.Name = name;
                user.PhoneNumber = phoneNumber;

                await context.SaveChangesAsync();

                return new ResponseModel
                {
                    Success = true,
                    Message = "Profile updated successfully."
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }

        public async Task<ResponseModel> UpdateProfilePictureAsync(string userId, string profilePicturePath)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Find user by UserId
                var user = await context.AdminInfos.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "User not found."
                    };
                }

                // Delete old profile picture if exists
                if (!string.IsNullOrEmpty(user.ProfilePicture))
                {
                    var oldPicturePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfilePicture.TrimStart('/'));
                    if (File.Exists(oldPicturePath))
                    {
                        try
                        {
                            File.Delete(oldPicturePath);
                        }
                        catch
                        {
                            // Ignore errors when deleting old picture
                        }
                    }
                }

                // Update profile picture path
                user.ProfilePicture = profilePicturePath;
                await context.SaveChangesAsync();

                return new ResponseModel
                {
                    Success = true,
                    Message = "Profile picture updated successfully."
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }

        // Leave Management Methods
        public async Task<List<EmploymentTypeDefinition>> GetEmploymentTypesForManagementAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                await EnsureDefaultEmploymentTypesAsync(context);
                return await context.EmploymentTypes
                    .AsNoTracking()
                    .OrderBy(e => e.DisplayOrder)
                    .ThenBy(e => e.Name)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<EmploymentTypeDefinition>();
            }
        }

        public async Task<ResponseModel> SaveEmploymentTypesConfigurationAsync(IReadOnlyList<EmploymentTypeManagementItem> items)
        {
            if (items == null || items.Count == 0)
                return new ResponseModel { Success = false, Message = "No employment types to save." };

            var names = items.Select(i => (i.Name ?? string.Empty).Trim()).Where(n => n.Length > 0).ToList();
            if (names.Count != items.Count)
                return new ResponseModel { Success = false, Message = "Each employment type must have a name." };
            if (names.Count != names.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                return new ResponseModel { Success = false, Message = "Employment type names must be unique." };

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                await EnsureDefaultEmploymentTypesAsync(context);

                foreach (var item in items)
                {
                    var name = item.Name.Trim();
                    if (name.Length > 100)
                        return new ResponseModel { Success = false, Message = "Name is too long." };

                    if (item.Id > 0)
                    {
                        var entity = await context.EmploymentTypes.FirstOrDefaultAsync(e => e.Id == item.Id);
                        if (entity == null)
                            return new ResponseModel { Success = false, Message = $"Employment type {item.Id} not found." };

                        var taken = await context.EmploymentTypes.AnyAsync(e => e.Id != item.Id && e.Name.ToLower() == name.ToLower());
                        if (taken)
                            return new ResponseModel { Success = false, Message = $"The name \"{name}\" is already used." };

                        entity.Name = name;
                        entity.DisplayOrder = item.DisplayOrder;
                        entity.IsActive = item.IsActive;
                    }
                    else
                    {
                        var taken = await context.EmploymentTypes.AnyAsync(e => e.Name.ToLower() == name.ToLower());
                        if (taken)
                            return new ResponseModel { Success = false, Message = $"The name \"{name}\" is already used." };

                        var code = await GenerateUniqueEmploymentTypeCodeAsync(context, name);
                        context.EmploymentTypes.Add(new EmploymentTypeDefinition
                        {
                            Name = name,
                            Code = code,
                            DisplayOrder = item.DisplayOrder,
                            IsActive = item.IsActive
                        });
                    }
                }

                await context.SaveChangesAsync();
                await EnsureLeaveAllocationMatrixForAllTypesAsync(context);

                var (vf, vt) = GetCurrentLeaveWindow(DateTime.Today);
                foreach (var empId in await context.AdminInfos.Select(e => e.Id).ToListAsync())
                    await EnsureLeaveBalancesForEmployeeAsync(context, empId, vf, vt);

                return new ResponseModel { Success = true, Message = "Employment types saved." };
            }
            catch (Exception ex)
            {
                return new ResponseModel { Success = false, Message = $"An error occurred: {ex.Message}" };
            }
        }

        public async Task<List<LeaveType>> GetActiveLeaveTypesAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                await EnsureDefaultLeaveTypesAsync(context);
                return await context.LeaveTypes
                    .AsNoTracking()
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.DisplayOrder)
                    .ThenBy(t => t.Name)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<LeaveType>();
            }
        }

        /// <summary>Leave types the employee may request, based on employment category and configured allocations.</summary>
        public async Task<List<LeaveType>> GetActiveLeaveTypesForEmployeeAsync(int employeeId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                await EnsureDefaultEmploymentTypesAsync(context);
                await EnsureDefaultLeaveTypesAsync(context);
                await EnsureLeaveAllocationMatrixForAllTypesAsync(context);

                var empTypeId = await context.AdminInfos
                    .AsNoTracking()
                    .Where(e => e.Id == employeeId)
                    .Select(e => e.EmploymentTypeId)
                    .FirstOrDefaultAsync();
                if (empTypeId <= 0)
                    return new List<LeaveType>();

                var allowedIds = await context.LeaveTypeEmploymentAllocations
                    .AsNoTracking()
                    .Where(a => a.EmploymentTypeId == empTypeId && a.IsAvailable && a.AllocationDays > 0)
                    .Select(a => a.LeaveTypeId)
                    .ToListAsync();

                return await context.LeaveTypes
                    .AsNoTracking()
                    .Where(t => t.IsActive && allowedIds.Contains(t.Id))
                    .OrderBy(t => t.DisplayOrder)
                    .ThenBy(t => t.Name)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<LeaveType>();
            }
        }

        public async Task<List<LeaveType>> GetLeaveTypesForManagementAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                await EnsureDefaultEmploymentTypesAsync(context);
                await EnsureDefaultLeaveTypesAsync(context);
                await EnsureLeaveAllocationMatrixForAllTypesAsync(context);
                return await context.LeaveTypes
                    .AsNoTracking()
                    .Include(t => t.EmploymentAllocations)
                    .OrderBy(t => t.DisplayOrder)
                    .ThenBy(t => t.Name)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<LeaveType>();
            }
        }

        public async Task<ResponseModel> SaveLeaveTypesConfigurationAsync(IReadOnlyList<LeaveTypeManagementItem> items)
        {
            if (items == null || items.Count == 0)
                return new ResponseModel { Success = false, Message = "No leave types to save." };

            var trimmedNames = items
                .Select(i => (i.Name ?? string.Empty).Trim())
                .Where(n => n.Length > 0)
                .ToList();
            if (trimmedNames.Count != items.Count)
                return new ResponseModel { Success = false, Message = "Each leave type must have a name." };
            if (trimmedNames.Count != trimmedNames.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                return new ResponseModel { Success = false, Message = "Leave type names must be unique." };

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                await EnsureDefaultEmploymentTypesAsync(context);
                await EnsureDefaultLeaveTypesAsync(context);

                foreach (var item in items)
                {
                    var name = item.Name.Trim();
                    if (name.Length > 100)
                        return new ResponseModel { Success = false, Message = $"Name too long: {name}." };

                    if (item.EmploymentAllocations == null || item.EmploymentAllocations.Count == 0)
                        return new ResponseModel { Success = false, Message = $"Configure at least one employment allocation for \"{name}\"." };

                    LeaveType entity;
                    if (item.Id > 0)
                    {
                        entity = await context.LeaveTypes.FirstOrDefaultAsync(t => t.Id == item.Id);
                        if (entity == null)
                            return new ResponseModel { Success = false, Message = $"Leave type id {item.Id} was not found." };

                        var nameTaken = await context.LeaveTypes.AnyAsync(t => t.Id != item.Id && t.Name.ToLower() == name.ToLower());
                        if (nameTaken)
                            return new ResponseModel { Success = false, Message = $"The name \"{name}\" is already used." };

                        entity.Name = name;
                        entity.RequiresDocumentForMultiDay = item.RequiresDocumentForMultiDay;
                        entity.DisplayOrder = item.DisplayOrder;
                        entity.IsActive = item.IsActive;
                    }
                    else
                    {
                        var nameTakenNew = await context.LeaveTypes.AnyAsync(t => t.Name.ToLower() == name.ToLower());
                        if (nameTakenNew)
                            return new ResponseModel { Success = false, Message = $"The name \"{name}\" is already used." };

                        var defaultDays = item.EmploymentAllocations.Max(a => a.AllocationDays);
                        entity = new LeaveType
                        {
                            Name = name,
                            DefaultAllocationDays = defaultDays,
                            RequiresDocumentForMultiDay = item.RequiresDocumentForMultiDay,
                            DisplayOrder = item.DisplayOrder,
                            IsActive = item.IsActive
                        };
                        context.LeaveTypes.Add(entity);
                        await context.SaveChangesAsync();
                    }

                    var maxAlloc = item.EmploymentAllocations.Max(a => a.AllocationDays);
                    entity.DefaultAllocationDays = maxAlloc;

                    var existingRows = await context.LeaveTypeEmploymentAllocations
                        .Where(a => a.LeaveTypeId == entity.Id)
                        .ToListAsync();
                    var wanted = item.EmploymentAllocations
                        .GroupBy(a => a.EmploymentTypeId)
                        .ToDictionary(g => g.Key, g => g.Last());

                    foreach (var row in existingRows)
                    {
                        if (!wanted.ContainsKey(row.EmploymentTypeId))
                            context.LeaveTypeEmploymentAllocations.Remove(row);
                    }

                    foreach (var kv in wanted)
                    {
                        var etId = kv.Key;
                        var al = kv.Value;
                        var row = await context.LeaveTypeEmploymentAllocations
                            .FirstOrDefaultAsync(a => a.LeaveTypeId == entity.Id && a.EmploymentTypeId == etId);
                        if (row == null)
                        {
                            context.LeaveTypeEmploymentAllocations.Add(new LeaveTypeEmploymentAllocation
                            {
                                LeaveTypeId = entity.Id,
                                EmploymentTypeId = etId,
                                AllocationDays = al.AllocationDays,
                                IsAvailable = al.IsAvailable
                            });
                        }
                        else
                        {
                            row.AllocationDays = al.AllocationDays;
                            row.IsAvailable = al.IsAvailable;
                        }
                    }
                }

                await context.SaveChangesAsync();

                var (vf, vt) = GetCurrentLeaveWindow(DateTime.Today);
                var employeeIds = await context.AdminInfos.Select(e => e.Id).ToListAsync();
                foreach (var empId in employeeIds)
                    await EnsureLeaveBalancesForEmployeeAsync(context, empId, vf, vt);

                return new ResponseModel { Success = true, Message = "Leave configuration saved." };
            }
            catch (Exception ex)
            {
                return new ResponseModel { Success = false, Message = $"An error occurred: {ex.Message}" };
            }
        }

        public async Task<ResponseModel> UpdateEmployeeEmploymentTypeAsync(int employeeId, int employmentTypeId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                await EnsureDefaultEmploymentTypesAsync(context);

                var typeExists = await context.EmploymentTypes.AnyAsync(e => e.Id == employmentTypeId && e.IsActive);
                if (!typeExists)
                    return new ResponseModel { Success = false, Message = "Invalid employment type." };

                var emp = await context.AdminInfos.FirstOrDefaultAsync(e => e.Id == employeeId);
                if (emp == null)
                    return new ResponseModel { Success = false, Message = "Employee not found." };

                emp.EmploymentTypeId = employmentTypeId;
                await context.SaveChangesAsync();

                var (vf, vt) = GetCurrentLeaveWindow(DateTime.Today);
                await EnsureLeaveBalancesForEmployeeAsync(context, employeeId, vf, vt);

                return new ResponseModel { Success = true, Message = "Employment type updated." };
            }
            catch (Exception ex)
            {
                return new ResponseModel { Success = false, Message = $"An error occurred: {ex.Message}" };
            }
        }

        public async Task<List<EmployeeLeaveSummaryDto>> GetEmployeeLeaveSummariesForAdminAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                await EnsureDefaultEmploymentTypesAsync(context);
                await EnsureDefaultLeaveTypesAsync(context);
                await EnsureLeaveAllocationMatrixForAllTypesAsync(context);

                var (validFrom, validTo) = GetCurrentLeaveWindow(DateTime.Today);

                var sickTypeId = await context.LeaveTypes
                    .AsNoTracking()
                    .Where(t => t.Name == "Sick Leave")
                    .Select(t => t.Id)
                    .FirstOrDefaultAsync();

                var employees = await context.AdminInfos.AsNoTracking().ToListAsync();
                foreach (var emp in employees)
                    await EnsureLeaveBalancesForEmployeeAsync(context, emp.Id, validFrom, validTo);

                var result = new List<EmployeeLeaveSummaryDto>();
                foreach (var emp in employees)
                {
                    var allowedLeaveTypeIds = await context.LeaveTypeEmploymentAllocations
                        .AsNoTracking()
                        .Where(a => a.EmploymentTypeId == emp.EmploymentTypeId && a.IsAvailable && a.AllocationDays > 0)
                        .Select(a => a.LeaveTypeId)
                        .ToListAsync();

                    var balances = await context.LeaveBalances
                        .AsNoTracking()
                        .Include(b => b.LeaveType)
                        .Where(b => b.EmployeeId == emp.Id && b.ValidFrom == validFrom)
                        .ToListAsync();

                    var filtered = balances
                        .Where(b => b.LeaveType != null && b.LeaveType.IsActive && allowedLeaveTypeIds.Contains(b.LeaveTypeId))
                        .ToList();

                    var totalAlloc = filtered.Sum(b => b.TotalAllocatedDays);
                    var totalRem = filtered.Sum(b => b.TotalAllocatedDays - b.UsedDays);
                    var sick = filtered.FirstOrDefault(b => b.LeaveTypeId == sickTypeId);
                    var sickAlloc = sick?.TotalAllocatedDays ?? 0m;
                    var sickRem = sick != null ? sick.TotalAllocatedDays - sick.UsedDays : 0m;
                    var vTo = filtered.Count > 0 ? filtered.Max(b => b.ValidTo) : validTo;
                    var vFrom = filtered.Count > 0 ? filtered.Min(b => b.ValidFrom) : validFrom;

                    var byType = filtered
                        .OrderBy(b => b.LeaveType!.DisplayOrder)
                        .ThenBy(b => b.LeaveType!.Name)
                        .Select(b => new EmployeeLeaveTypeBalanceDto
                        {
                            LeaveTypeId = b.LeaveTypeId,
                            LeaveTypeName = b.LeaveType!.Name,
                            AllocatedDays = b.TotalAllocatedDays,
                            UsedDays = b.UsedDays,
                            RemainingDays = b.TotalAllocatedDays - b.UsedDays
                        })
                        .ToList();

                    result.Add(new EmployeeLeaveSummaryDto
                    {
                        EmployeeId = emp.Id,
                        LeaveTypes = byType,
                        TotalAllocatedDays = totalAlloc,
                        TotalRemainingDays = totalRem,
                        SickAllocatedDays = sickAlloc,
                        SickRemainingDays = sickRem,
                        ValidFrom = vFrom,
                        ValidTo = vTo
                    });
                }

                return result;
            }
            catch (Exception)
            {
                return new List<EmployeeLeaveSummaryDto>();
            }
        }

        public async Task<ResponseModel> UpdateEmployeeLeaveValidUntilAsync(int employeeId, DateTime newValidTo)
        {
            try
            {
                var (validFrom, defaultValidTo) = GetCurrentLeaveWindow(DateTime.Today);
                if (newValidTo.Date < validFrom.Date)
                    return new ResponseModel { Success = false, Message = "Valid until must be on or after the leave period start date." };

                using var context = await _contextFactory.CreateDbContextAsync();
                await EnsureDefaultEmploymentTypesAsync(context);
                await EnsureDefaultLeaveTypesAsync(context);
                await EnsureLeaveAllocationMatrixForAllTypesAsync(context);

                var emp = await context.AdminInfos.FirstOrDefaultAsync(e => e.Id == employeeId);
                if (emp == null)
                    return new ResponseModel { Success = false, Message = "Employee not found." };

                await EnsureLeaveBalancesForEmployeeAsync(context, employeeId, validFrom, defaultValidTo);
                await context.SaveChangesAsync();

                var rows = await context.LeaveBalances
                    .Where(b => b.EmployeeId == employeeId && b.ValidFrom == validFrom)
                    .ToListAsync();

                foreach (var r in rows)
                {
                    r.ValidTo = newValidTo.Date;
                    r.UpdatedOn = DateTime.Now;
                }

                await context.SaveChangesAsync();
                return new ResponseModel { Success = true, Message = "Leave valid until date updated." };
            }
            catch (Exception ex)
            {
                return new ResponseModel { Success = false, Message = $"An error occurred: {ex.Message}" };
            }
        }

        public async Task<ResponseModel> UpdateEmployeeInfoRowAsync(int employeeId, int employmentTypeId, DateTime leaveValidUntil)
        {
            var empResult = await UpdateEmployeeEmploymentTypeAsync(employeeId, employmentTypeId);
            if (!empResult.Success)
                return empResult;

            var dateResult = await UpdateEmployeeLeaveValidUntilAsync(employeeId, leaveValidUntil);
            if (!dateResult.Success)
                return dateResult;

            return new ResponseModel { Success = true, Message = "Employee info saved." };
        }

        public async Task<List<LeaveBalance>> GetLeaveBalancesForEmployeeAsync(int employeeId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                await EnsureDefaultEmploymentTypesAsync(context);
                await EnsureDefaultLeaveTypesAsync(context);
                await EnsureLeaveAllocationMatrixForAllTypesAsync(context);

                var (validFrom, validTo) = GetCurrentLeaveWindow(DateTime.Today);
                await EnsureLeaveBalancesForEmployeeAsync(context, employeeId, validFrom, validTo);

                var empTypeId = await context.AdminInfos
                    .AsNoTracking()
                    .Where(e => e.Id == employeeId)
                    .Select(e => e.EmploymentTypeId)
                    .FirstOrDefaultAsync();

                var allowedLeaveTypeIds = await context.LeaveTypeEmploymentAllocations
                    .AsNoTracking()
                    .Where(a => a.EmploymentTypeId == empTypeId && a.IsAvailable && a.AllocationDays > 0)
                    .Select(a => a.LeaveTypeId)
                    .ToListAsync();

                var list = await context.LeaveBalances
                    .AsNoTracking()
                    .Include(b => b.LeaveType)
                    .Where(b => b.EmployeeId == employeeId && b.ValidFrom == validFrom)
                    .OrderBy(b => b.LeaveType.DisplayOrder)
                    .ThenBy(b => b.LeaveType.Name)
                    .ToListAsync();

                return list
                    .Where(b => b.LeaveType != null && b.LeaveType.IsActive && allowedLeaveTypeIds.Contains(b.LeaveTypeId))
                    .ToList();
            }
            catch (Exception)
            {
                return new List<LeaveBalance>();
            }
        }

        public async Task<List<LeaveApplication>> GetLeaveApplicationsForEmployeeAsync(int employeeId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.LeaveApplications
                    .AsNoTracking()
                    .Include(a => a.LeaveType)
                    .Include(a => a.ReviewedByEmployee)
                    .Where(a => a.EmployeeId == employeeId)
                    .OrderByDescending(a => a.AppliedOn)
                    .ThenByDescending(a => a.Id)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<LeaveApplication>();
            }
        }

        public async Task<List<LeaveApplication>> GetAllLeaveApplicationsAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.LeaveApplications
                    .AsNoTracking()
                    .Include(a => a.Employee)
                    .Include(a => a.LeaveType)
                    .Include(a => a.ReviewedByEmployee)
                    .OrderBy(a => a.Status == "Pending" ? 0 : 1)
                    .ThenByDescending(a => a.AppliedOn)
                    .ThenByDescending(a => a.Id)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<LeaveApplication>();
            }
        }

        public async Task<ResponseModel> ApplyLeaveAsync(int employeeId, int leaveTypeId, DateTime startDate, DateTime endDate, string reason, string? supportingDocumentPath)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                await EnsureLeaveAllocationMatrixForAllTypesAsync(context);

                if (employeeId <= 0)
                    return new ResponseModel { Success = false, Message = "Invalid employee." };

                if (startDate.Date > endDate.Date)
                    return new ResponseModel { Success = false, Message = "Start date cannot be after end date." };

                if (string.IsNullOrWhiteSpace(reason))
                    return new ResponseModel { Success = false, Message = "Reason is required." };

                if (reason.Trim().Length > 2000)
                    return new ResponseModel { Success = false, Message = "Reason is too long (maximum 2000 characters)." };

                var leaveType = await context.LeaveTypes
                    .FirstOrDefaultAsync(t => t.Id == leaveTypeId && t.IsActive);
                if (leaveType == null)
                    return new ResponseModel { Success = false, Message = "Invalid leave type selected." };

                var employeeRow = await context.AdminInfos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == employeeId);
                if (employeeRow == null)
                    return new ResponseModel { Success = false, Message = "User not found." };

                var allocOk = await context.LeaveTypeEmploymentAllocations
                    .AsNoTracking()
                    .AnyAsync(a =>
                        a.LeaveTypeId == leaveTypeId &&
                        a.EmploymentTypeId == employeeRow.EmploymentTypeId &&
                        a.IsAvailable &&
                        a.AllocationDays > 0);
                if (!allocOk)
                    return new ResponseModel { Success = false, Message = "This leave type is not available for your employment category." };
                
                var isSickLeave = string.Equals(leaveType.Name, "Sick Leave", StringComparison.OrdinalIgnoreCase);
                var today = DateTime.Today;
                if (isSickLeave && (startDate.Date > today || endDate.Date > today))
                    return new ResponseModel { Success = false, Message = "For Sick Leave, only today or previous dates are allowed." };
                if (!isSickLeave && (startDate.Date < today || endDate.Date < today))
                    return new ResponseModel { Success = false, Message = "For this leave type, past dates are not allowed." };

                var durationDays = CalculateDurationDays(startDate, endDate);
                if (durationDays <= 0)
                    return new ResponseModel { Success = false, Message = "Leave duration must be at least 1 day." };

                if (isSickLeave && durationDays > 1 && string.IsNullOrWhiteSpace(supportingDocumentPath))
                    return new ResponseModel { Success = false, Message = "Supporting document is required for multi-day sick leave." };

                var conflicting = await context.LeaveApplications
                    .AsNoTracking()
                    .Include(a => a.LeaveType)
                    .Where(a => a.EmployeeId == employeeId
                        && a.Status != "Rejected"
                        && a.StartDate <= endDate.Date
                        && a.EndDate >= startDate.Date)
                    .OrderBy(a => a.StartDate)
                    .FirstOrDefaultAsync();
                if (conflicting != null)
                {
                    var typeName = conflicting.LeaveType?.Name ?? "leave";
                    var statusLabel = string.Equals(conflicting.Status, "Pending", StringComparison.OrdinalIgnoreCase)
                        ? "pending"
                        : conflicting.Status.ToLowerInvariant();
                    return new ResponseModel
                    {
                        Success = false,
                        Message = $"These dates overlap an existing {statusLabel} {typeName} request ({conflicting.StartDate:dd MMM yyyy} – {conflicting.EndDate:dd MMM yyyy}). Choose different dates that do not overlap."
                    };
                }

                var (validFrom, validTo) = GetCurrentLeaveWindow(startDate.Date);

                await EnsureLeaveBalancesForEmployeeAsync(context, employeeId, validFrom, validTo);
                await RecalculateLeaveUsageAsync(context, employeeId, leaveTypeId, validFrom);

                var balance = await context.LeaveBalances
                    .FirstOrDefaultAsync(b =>
                        b.EmployeeId == employeeId &&
                        b.LeaveTypeId == leaveTypeId &&
                        b.ValidFrom == validFrom);
                if (balance == null)
                    return new ResponseModel { Success = false, Message = "Unable to locate leave balance for the selected period." };

                if (startDate.Date < balance.ValidFrom || endDate.Date > balance.ValidTo)
                    return new ResponseModel { Success = false, Message = $"Selected dates must be within the leave period ({balance.ValidFrom:yyyy-MM-dd} to {balance.ValidTo:yyyy-MM-dd})." };

                var pendingDays = await context.LeaveApplications
                    .AsNoTracking()
                    .Where(a => a.EmployeeId == employeeId
                        && a.LeaveTypeId == leaveTypeId
                        && a.Status == "Pending"
                        && a.StartDate >= balance.ValidFrom
                        && a.EndDate <= balance.ValidTo)
                    .SumAsync(a => (decimal?)a.DurationDays) ?? 0m;

                var availableDays = balance.TotalAllocatedDays - balance.UsedDays - pendingDays;
                if (durationDays > availableDays)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = $"Insufficient leave balance. Available: {availableDays:0.##} day(s), Requested: {durationDays:0.##} day(s)."
                    };
                }

                var application = new LeaveApplication
                {
                    EmployeeId = employeeId,
                    LeaveTypeId = leaveTypeId,
                    StartDate = startDate.Date,
                    EndDate = endDate.Date,
                    DurationDays = durationDays,
                    Reason = reason.Trim(),
                    SupportingDocumentPath = string.IsNullOrWhiteSpace(supportingDocumentPath) ? null : supportingDocumentPath.Trim(),
                    Status = "Pending",
                    AppliedOn = DateTime.Now
                };

                await context.LeaveApplications.AddAsync(application);
                await context.SaveChangesAsync();

                return new ResponseModel { Success = true, Message = "Leave application submitted successfully." };
            }
            catch (Exception ex)
            {
                return new ResponseModel { Success = false, Message = $"An error occurred: {ex.Message}" };
            }
        }

        public async Task<ResponseModel> ReviewLeaveApplicationAsync(int applicationId, int reviewerEmployeeId, bool approve, string? reviewerComments)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var application = await context.LeaveApplications
                    .Include(a => a.LeaveType)
                    .FirstOrDefaultAsync(a => a.Id == applicationId);

                if (application == null)
                    return new ResponseModel { Success = false, Message = "Leave application not found." };

                if (!string.Equals(application.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                    return new ResponseModel { Success = false, Message = "Only pending applications can be reviewed." };

                var (validFrom, validTo) = GetCurrentLeaveWindow(application.StartDate);
                await EnsureLeaveBalancesForEmployeeAsync(context, application.EmployeeId, validFrom, validTo);
                await RecalculateLeaveUsageAsync(context, application.EmployeeId, application.LeaveTypeId, validFrom);

                if (approve)
                {
                    var balance = await context.LeaveBalances
                        .FirstOrDefaultAsync(b =>
                            b.EmployeeId == application.EmployeeId &&
                            b.LeaveTypeId == application.LeaveTypeId &&
                            b.ValidFrom == validFrom);
                    if (balance == null)
                        return new ResponseModel { Success = false, Message = "Unable to locate leave balance for approval." };

                    var remainingBeforeApproval = balance.TotalAllocatedDays - balance.UsedDays;
                    if (application.DurationDays > remainingBeforeApproval)
                    {
                        return new ResponseModel
                        {
                            Success = false,
                            Message = $"Cannot approve. Remaining balance is {remainingBeforeApproval:0.##} day(s) but requested {application.DurationDays:0.##} day(s)."
                        };
                    }

                    balance.UsedDays += application.DurationDays;
                    balance.UpdatedOn = DateTime.Now;
                    application.Status = "Approved";
                }
                else
                {
                    application.Status = "Rejected";
                }

                application.ReviewedByEmployeeId = reviewerEmployeeId > 0 ? reviewerEmployeeId : null;
                application.ReviewedOn = DateTime.Now;
                application.ReviewerComments = string.IsNullOrWhiteSpace(reviewerComments) ? null : reviewerComments.Trim();

                await context.SaveChangesAsync();

                return new ResponseModel
                {
                    Success = true,
                    Message = approve ? "Leave application approved." : "Leave application rejected."
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel { Success = false, Message = $"An error occurred: {ex.Message}" };
            }
        }

        private static decimal CalculateDurationDays(DateTime startDate, DateTime endDate)
        {
            return (decimal)(endDate.Date - startDate.Date).TotalDays + 1;
        }

        private static (DateTime validFrom, DateTime validTo) GetCurrentLeaveWindow(DateTime referenceDate)
        {
            var validFrom = new DateTime(referenceDate.Year, 1, 1);
            var validTo = new DateTime(referenceDate.Year, 12, 31);
            return (validFrom, validTo);
        }

        private async Task EnsureDefaultEmploymentTypesAsync(RegesterServiceContext context)
        {
            if (await context.EmploymentTypes.AnyAsync())
                return;

            context.EmploymentTypes.AddRange(
                new EmploymentTypeDefinition { Name = "Full-Time", Code = "FullTime", DisplayOrder = 1, IsActive = true },
                new EmploymentTypeDefinition { Name = "Intern", Code = "Intern", DisplayOrder = 2, IsActive = true });
            await context.SaveChangesAsync();
        }

        private async Task EnsureDefaultLeaveTypesAsync(RegesterServiceContext context)
        {
            if (await context.LeaveTypes.AnyAsync())
                return;

            await context.LeaveTypes.AddRangeAsync(new[]
            {
                new LeaveType { Name = "Sick Leave", DefaultAllocationDays = 10, RequiresDocumentForMultiDay = true, IsActive = true, DisplayOrder = 1 },
                new LeaveType { Name = "Annual Leave", DefaultAllocationDays = 14, RequiresDocumentForMultiDay = false, IsActive = true, DisplayOrder = 2 },
                new LeaveType { Name = "Casual Leave", DefaultAllocationDays = 10, RequiresDocumentForMultiDay = false, IsActive = true, DisplayOrder = 3 }
            });
            await context.SaveChangesAsync();
        }

        private async Task EnsureLeaveAllocationMatrixForAllTypesAsync(RegesterServiceContext context)
        {
            await EnsureDefaultEmploymentTypesAsync(context);
            await EnsureDefaultLeaveTypesAsync(context);

            var empIds = await context.EmploymentTypes.Where(e => e.IsActive).Select(e => e.Id).ToListAsync();
            var leaveTypes = await context.LeaveTypes.ToListAsync();

            foreach (var lt in leaveTypes)
            {
                foreach (var etId in empIds)
                {
                    var exists = await context.LeaveTypeEmploymentAllocations
                        .AnyAsync(a => a.LeaveTypeId == lt.Id && a.EmploymentTypeId == etId);
                    if (exists)
                        continue;

                    context.LeaveTypeEmploymentAllocations.Add(new LeaveTypeEmploymentAllocation
                    {
                        LeaveTypeId = lt.Id,
                        EmploymentTypeId = etId,
                        AllocationDays = lt.DefaultAllocationDays,
                        IsAvailable = true
                    });
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task<string> GenerateUniqueEmploymentTypeCodeAsync(RegesterServiceContext context, string name)
        {
            var baseCode = new string((name ?? "Type").Where(char.IsLetterOrDigit).ToArray());
            if (string.IsNullOrEmpty(baseCode))
                baseCode = "Type";
            if (baseCode.Length > 40)
                baseCode = baseCode[..40];

            var code = baseCode;
            var n = 0;
            while (await context.EmploymentTypes.AnyAsync(e => e.Code == code))
            {
                n++;
                code = baseCode + n;
                if (code.Length > 50)
                    code = code[..50];
            }

            return code;
        }

        private async Task EnsureLeaveBalancesForEmployeeAsync(RegesterServiceContext context, int employeeId, DateTime validFrom, DateTime validTo)
        {
            await EnsureLeaveAllocationMatrixForAllTypesAsync(context);

            var empTypeId = await context.AdminInfos
                .AsNoTracking()
                .Where(e => e.Id == employeeId)
                .Select(e => e.EmploymentTypeId)
                .FirstOrDefaultAsync();

            var pairs = await context.LeaveTypeEmploymentAllocations
                .Include(a => a.LeaveType)
                .Where(a =>
                    a.EmploymentTypeId == empTypeId &&
                    a.IsAvailable &&
                    a.AllocationDays > 0 &&
                    a.LeaveType.IsActive)
                .ToListAsync();

            foreach (var p in pairs)
            {
                var alloc = p.AllocationDays;
                var typeId = p.LeaveTypeId;

                var balance = await context.LeaveBalances
                    .FirstOrDefaultAsync(b =>
                        b.EmployeeId == employeeId &&
                        b.LeaveTypeId == typeId &&
                        b.ValidFrom == validFrom);

                if (balance == null)
                {
                    context.LeaveBalances.Add(new LeaveBalance
                    {
                        EmployeeId = employeeId,
                        LeaveTypeId = typeId,
                        TotalAllocatedDays = alloc,
                        UsedDays = 0,
                        ValidFrom = validFrom,
                        ValidTo = validTo,
                        UpdatedOn = DateTime.Now
                    });
                }
                else if (balance.TotalAllocatedDays != alloc)
                {
                    balance.TotalAllocatedDays = alloc;
                    balance.UpdatedOn = DateTime.Now;
                }
            }

            await context.SaveChangesAsync();
        }

        private async Task RecalculateLeaveUsageAsync(RegesterServiceContext context, int employeeId, int leaveTypeId, DateTime validFrom)
        {
            var balance = await context.LeaveBalances
                .FirstOrDefaultAsync(b =>
                    b.EmployeeId == employeeId &&
                    b.LeaveTypeId == leaveTypeId &&
                    b.ValidFrom == validFrom);
            if (balance == null)
                return;

            var vf = balance.ValidFrom;
            var vt = balance.ValidTo;

            var approvedUsed = await context.LeaveApplications
                .Where(a => a.EmployeeId == employeeId
                    && a.LeaveTypeId == leaveTypeId
                    && a.Status == "Approved"
                    && a.StartDate >= vf
                    && a.EndDate <= vt)
                .SumAsync(a => (decimal?)a.DurationDays) ?? 0m;

            if (balance.UsedDays != approvedUsed)
            {
                balance.UsedDays = approvedUsed;
                balance.UpdatedOn = DateTime.Now;
                await context.SaveChangesAsync();
            }
        }

        // Menu Management Methods
        public async Task<List<MenuItem>> GetAllMenuItemsAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.MenuItems
                    .AsNoTracking()
                    .Where(m => !StaticMenuUrls.Contains((m.Url ?? "").Trim()))
                    .Include(m => m.Parent)
                    .Include(m => m.Children)
                    .OrderBy(m => m.DisplayOrder)
                    .ThenBy(m => m.Name)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<MenuItem>();
            }
        }

        public async Task<List<MenuItem>> GetActiveMenuItemsAsync(bool isAuthenticated = false)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                // Get only top-level menu items (ParentId is null) that are active
                var query = context.MenuItems
                    .AsNoTracking()
                    .Where(m => m.Status == true && m.ParentId == null && !StaticMenuUrls.Contains((m.Url ?? "").Trim()));
                
                // Filter by authentication requirement
                if (!isAuthenticated)
                {
                    query = query.Where(m => m.RequiresAuthentication == false);
                }
                
                var topLevelItems = await query
                    .OrderBy(m => m.DisplayOrder)
                    .ThenBy(m => m.Name)
                    .ToListAsync();
                
                // Load and filter children based on authentication
                foreach (var item in topLevelItems)
                {
                    var childrenQuery = context.MenuItems
                        .AsNoTracking()
                        .Where(c => c.ParentId == item.Id && c.Status == true && !StaticMenuUrls.Contains((c.Url ?? "").Trim()));
                    
                    if (!isAuthenticated)
                    {
                        childrenQuery = childrenQuery.Where(c => c.RequiresAuthentication == false);
                    }
                    
                    var children = await childrenQuery
                        .OrderBy(c => c.DisplayOrder)
                        .ThenBy(c => c.Name)
                        .ToListAsync();
                    
                    item.Children = children;
                }
                
                return topLevelItems;
            }
            catch (Exception)
            {
                return new List<MenuItem>();
            }
        }

        public async Task<List<DbModels.Role>> GetRolesForEmployeeAsync(int employeeId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var user = await context.AdminInfos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == employeeId);

                if (user == null || string.IsNullOrEmpty(user.UserId))
                {
                    return new List<DbModels.Role>();
                }

                var roles = await context.UserRoles
                    .AsNoTracking()
                    .Include(ur => ur.Role)
                    .Where(ur => ur.UserId == user.UserId && ur.Role != null)
                    .Select(ur => ur.Role!)
                    .Distinct()
                    .OrderBy(r => r.RoleName)
                    .ToListAsync();

                return roles;
            }
            catch (Exception)
            {
                return new List<DbModels.Role>();
            }
        }

        public async Task<List<int>> GetMenuPermissionsForRoleAsync(int roleId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.RoleMenuPermissions
                    .AsNoTracking()
                    .Where(rp => rp.RoleId == roleId)
                    .Select(rp => rp.MenuItemId)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<int>();
            }
        }

        public async Task<ResponseModel> SetMenuPermissionsForRoleAsync(int roleId, List<int> menuItemIds)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var roleExists = await context.Roles
                    .AsNoTracking()
                    .AnyAsync(r => r.Id == roleId);
                if (!roleExists)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Role not found."
                    };
                }

                var validMenuIds = await context.MenuItems
                    .AsNoTracking()
                    .Where(m => menuItemIds.Contains(m.Id))
                    .Select(m => m.Id)
                    .ToListAsync();

                var existing = await context.RoleMenuPermissions
                    .Where(rp => rp.RoleId == roleId)
                    .ToListAsync();

                context.RoleMenuPermissions.RemoveRange(existing);

                foreach (var menuId in validMenuIds.Distinct())
                {
                    context.RoleMenuPermissions.Add(new RoleMenuPermission
                    {
                        RoleId = roleId,
                        MenuItemId = menuId
                    });
                }

                await context.SaveChangesAsync();

                return new ResponseModel
                {
                    Success = true,
                    Message = "Menu permissions updated successfully."
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel
                {
                    Success = false,
                    Message = $"An error occurred while saving permissions: {ex.Message}"
                };
            }
        }

        public async Task<List<MenuItem>> GetActiveMenuItemsByRoleAsync(bool isAuthenticated, string? roleName)
        {
            try
            {
                var baseMenus = await GetActiveMenuItemsAsync(isAuthenticated);

                if (!isAuthenticated || string.IsNullOrWhiteSpace(roleName))
                {
                    return baseMenus;
                }

                using var context = await _contextFactory.CreateDbContextAsync();

                var role = await context.Roles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.RoleName == roleName);

                if (role == null)
                {
                    return baseMenus;
                }

                var allowedMenuIds = await context.RoleMenuPermissions
                    .AsNoTracking()
                    .Where(rp => rp.RoleId == role.Id)
                    .Select(rp => rp.MenuItemId)
                    .ToListAsync();

                if (!allowedMenuIds.Any())
                {
                    return new List<MenuItem>();
                }

                bool IsMenuAllowed(MenuItem item)
                {
                    if (allowedMenuIds.Contains(item.Id))
                        return true;

                    if (item.Children == null || !item.Children.Any())
                        return false;

                    return item.Children.Any(child => IsMenuAllowed(child));
                }

                var filteredTopLevel = new List<MenuItem>();

                foreach (var item in baseMenus)
                {
                    if (!IsMenuAllowed(item))
                    {
                        continue;
                    }

                    var allowedChildren = item.Children?
                        .Where(c => IsMenuAllowed(c))
                        .OrderBy(c => c.DisplayOrder)
                        .ThenBy(c => c.Name)
                        .ToList() ?? new List<MenuItem>();

                    item.Children = allowedChildren;
                    filteredTopLevel.Add(item);
                }

                return filteredTopLevel;
            }
            catch (Exception)
            {
                return await GetActiveMenuItemsAsync(isAuthenticated);
            }
        }

        public async Task<List<MenuItem>> GetParentMenuItemsAsync(int? excludeId = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var query = context.MenuItems
                    .AsNoTracking()
                    .Where(m => m.ParentId == null && !StaticMenuUrls.Contains((m.Url ?? "").Trim())); // Only top-level items can be parents
                
                if (excludeId.HasValue)
                {
                    query = query.Where(m => m.Id != excludeId.Value); // Exclude current item to prevent circular reference
                }
                
                return await query
                    .OrderBy(m => m.DisplayOrder)
                    .ThenBy(m => m.Name)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<MenuItem>();
            }
        }

        public async Task<MenuItem?> GetMenuItemByIdAsync(int id)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.MenuItems
                    .AsNoTracking()
                    .Include(m => m.Parent)
                    .Include(m => m.Children)
                    .FirstOrDefaultAsync(m => m.Id == id);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<ResponseModel> CreateMenuItemAsync(string name, string url, bool status, string? icon, int displayOrder, int? parentId = null, bool requiresAuthentication = false)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var normalizedUrl = (url ?? "").Trim();

                // Validate input
                if (string.IsNullOrWhiteSpace(name))
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Menu name is required."
                    };
                }

                if (name.Length > 100)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Menu name is too long (maximum 100 characters)."
                    };
                }

                if (!string.IsNullOrWhiteSpace(url) && url.Length > 500)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Menu URL is too long (maximum 500 characters)."
                    };
                }
                
                if (StaticMenuUrls.Contains(normalizedUrl))
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "This route is a fixed system menu and cannot be managed here."
                    };
                }

                // Validate parent if provided
                if (parentId.HasValue && parentId.Value > 0)
                {
                    var parentExists = await context.MenuItems
                        .AsNoTracking()
                        .AnyAsync(m => m.Id == parentId.Value);
                    if (!parentExists)
                    {
                        return new ResponseModel
                        {
                            Success = false,
                            Message = "Selected parent menu item does not exist."
                        };
                    }
                }

                var menuItem = new MenuItem
                {
                    Name = name.Trim(),
                    Url = normalizedUrl,
                    Status = status,
                    Icon = icon?.Trim(),
                    DisplayOrder = displayOrder,
                    ParentId = parentId > 0 ? parentId : null,
                    RequiresAuthentication = requiresAuthentication,
                    CreatedDate = DateTime.Now
                };

                await context.MenuItems.AddAsync(menuItem);
                await context.SaveChangesAsync();

                return new ResponseModel
                {
                    Success = true,
                    Message = "Menu item created successfully."
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }

        public async Task<ResponseModel> UpdateMenuItemAsync(int id, string name, string url, bool status, string? icon, int displayOrder, int? parentId = null, bool requiresAuthentication = false)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var normalizedUrl = (url ?? "").Trim();

                var menuItem = await context.MenuItems.FirstOrDefaultAsync(m => m.Id == id);
                if (menuItem == null)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Menu item not found."
                    };
                }

                // Validate input
                if (string.IsNullOrWhiteSpace(name))
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Menu name is required."
                    };
                }

                if (name.Length > 100)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Menu name is too long (maximum 100 characters)."
                    };
                }

                if (!string.IsNullOrWhiteSpace(url) && url.Length > 500)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Menu URL is too long (maximum 500 characters)."
                    };
                }
                
                if (StaticMenuUrls.Contains((menuItem.Url ?? "").Trim()) || StaticMenuUrls.Contains(normalizedUrl))
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "This route is a fixed system menu and cannot be managed here."
                    };
                }

                // Validate parent if provided (prevent circular reference)
                if (parentId.HasValue && parentId.Value > 0)
                {
                    if (parentId.Value == id)
                    {
                        return new ResponseModel
                        {
                            Success = false,
                            Message = "A menu item cannot be its own parent."
                        };
                    }

                    // Check if parent exists
                    var parentExists = await context.MenuItems
                        .AsNoTracking()
                        .AnyAsync(m => m.Id == parentId.Value);
                    if (!parentExists)
                    {
                        return new ResponseModel
                        {
                            Success = false,
                            Message = "Selected parent menu item does not exist."
                        };
                    }

                    // Check for circular reference (prevent setting parent to a descendant)
                    var isDescendant = await IsDescendantAsync(context, parentId.Value, id);
                    if (isDescendant)
                    {
                        return new ResponseModel
                        {
                            Success = false,
                            Message = "Cannot set parent: this would create a circular reference."
                        };
                    }
                }

                menuItem.Name = name.Trim();
                menuItem.Url = normalizedUrl;
                menuItem.Status = status;
                menuItem.Icon = icon?.Trim();
                menuItem.DisplayOrder = displayOrder;
                menuItem.ParentId = parentId > 0 ? parentId : null;
                menuItem.RequiresAuthentication = requiresAuthentication;
                menuItem.UpdatedDate = DateTime.Now;

                await context.SaveChangesAsync();

                return new ResponseModel
                {
                    Success = true,
                    Message = "Menu item updated successfully."
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }

        public async Task<ResponseModel> DeleteMenuItemAsync(int id)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var menuItem = await context.MenuItems
                    .Include(m => m.Children)
                    .FirstOrDefaultAsync(m => m.Id == id);
                if (menuItem == null)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Menu item not found."
                    };
                }
                
                if (StaticMenuUrls.Contains((menuItem.Url ?? "").Trim()))
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "This route is a fixed system menu and cannot be managed here."
                    };
                }

                // Check if menu item has children
                if (menuItem.Children != null && menuItem.Children.Any())
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Cannot delete menu item. It has child menu items. Please delete or reassign children first."
                    };
                }

                context.MenuItems.Remove(menuItem);
                await context.SaveChangesAsync();

                return new ResponseModel
                {
                    Success = true,
                    Message = "Menu item deleted successfully."
                };
            }
            catch (Exception ex)
            {
                return new ResponseModel
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }

        // Helper method to check if a menu item is a descendant of another
        private async Task<bool> IsDescendantAsync(RegesterServiceContext context, int potentialParentId, int itemId)
        {
            var current = await context.MenuItems
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == potentialParentId);
            
            if (current == null) return false;
            
            // Traverse up the parent chain
            while (current.ParentId.HasValue)
            {
                if (current.ParentId.Value == itemId)
                {
                    return true; // Found circular reference
                }
                current = await context.MenuItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == current.ParentId.Value);
                if (current == null) break;
            }
            
            return false;
        }
    }
}

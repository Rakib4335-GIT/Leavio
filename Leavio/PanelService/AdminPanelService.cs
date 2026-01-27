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
                        .FirstOrDefaultAsync(x => x.UserId == registrationModel.UserId);
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
                    .FirstOrDefaultAsync(x => x.Email == registrationModel.Email);
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
                
                var dbAdminInfo = new AdminInfo
                {
                    UserId = registrationModel.UserId,
                    Name = registrationModel.Name,
                    Email = registrationModel.Email,
                    Password = registrationModel.Password
                    // RoleId is optional - we use User_Role table instead
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
                        Message = "Email is required."
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

                var user = await context.AdminInfos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Email == loginModel.EmailId);
                    
                if (user == null)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Email Not Registered"
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

                return new ResponseModel
                {
                    Success = true,
                    Message = $"Login Successful! User ID: {user.Id} | Name: {user.Name} | Email: {user.Email} | Role: {roleName}"
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
                return await context.AdminInfos
                    .AsNoTracking()
                    .OrderBy(u => u.Name)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<AdminInfo>();
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
    }
}

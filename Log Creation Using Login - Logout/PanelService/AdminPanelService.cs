using Log_Creation_Using_Login___Logout.DbModels;
using Log_Creation_Using_Login___Logout.Models;
using Microsoft.EntityFrameworkCore;

namespace Log_Creation_Using_Login___Logout.PanelService
{
    public class AdminPanelService
    {
        private readonly IDbContextFactory<RegesterServiceContext> _contextFactory;
        
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

                var existingAdmin = await context.AdminInfos.FirstOrDefaultAsync(x => x.Email == registrationModel.Email);
                if (existingAdmin != null)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Admin with this email already exists."
                    };
                }

                // Validate RoleId exists
                var roleExists = await context.Roles.AnyAsync(x => x.Id == registrationModel.RoleId);
                if (!roleExists)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "Invalid role selected."
                    };
                }
                
                var dbAdminInfo = new AdminInfo
                {
                    Name = registrationModel.Name,
                    Email = registrationModel.Email,
                    Password = registrationModel.Password
                    // RoleId is optional - we use User_Role table instead
                };
                
                await context.AdminInfos.AddAsync(dbAdminInfo);
                await context.SaveChangesAsync();

                // Create User_Role entry (primary source for role assignment)
                var userRole = new UserRole
                {
                    UserId = dbAdminInfo.Id,
                    RoleId = registrationModel.RoleId
                };
                await context.UserRoles.AddAsync(userRole);
                await context.SaveChangesAsync();

                return new ResponseModel
                {
                    Success = true,
                    Message = "Registration Successful."
                };
            }
            catch (DbUpdateException ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                return new ResponseModel
                {
                    Success = false,
                    Message = $"Database error: {innerMessage}"
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
                    .Include(ur => ur.Role)
                    .FirstOrDefaultAsync(ur => ur.UserId == user.Id);

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
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Roles.OrderBy(r => r.RoleName).ToListAsync();
            }
            catch (Exception)
            {
                return new List<DbModels.Role>();
            }
        }

        public async Task<List<UserRole>> GetAllUserRolesAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.UserRoles
                    .Include(ur => ur.User)
                    .Include(ur => ur.Role)
                    .OrderBy(ur => ur.User.Name)
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

                // Check if user exists
                var userExists = await context.AdminInfos.AnyAsync(u => u.Id == userId);
                if (!userExists)
                {
                    return new ResponseModel
                    {
                        Success = false,
                        Message = "User not found."
                    };
                }

                // Check if role exists
                var roleExists = await context.Roles.AnyAsync(r => r.Id == roleId);
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
                    .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
                
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
                    UserId = userId,
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
                return await context.AdminInfos.OrderBy(u => u.Name).ToListAsync();
            }
            catch (Exception)
            {
                return new List<AdminInfo>();
            }
        }
    }
}

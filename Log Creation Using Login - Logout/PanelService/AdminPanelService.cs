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
                
                var dbAdminInfo = new AdminInfo
                {
                    Name = registrationModel.Name,
                    Email = registrationModel.Email,
                    Password = registrationModel.Password
                };
                
                await context.AdminInfos.AddAsync(dbAdminInfo);
                await context.SaveChangesAsync();

                return new ResponseModel
                {
                    Success = true,
                    Message = "Registration Successful."
                };
            }
            catch (DbUpdateException ex)
            {
                // Get the inner exception message for more details
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
    }
}

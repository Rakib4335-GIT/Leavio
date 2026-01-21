using Log_Creation_Using_Login___Logout.DbModels;
using Log_Creation_Using_Login___Logout.Models;
using Microsoft.EntityFrameworkCore;

namespace Log_Creation_Using_Login___Logout.PanelService
{
    public class AdminPanelService
    {
        private readonly RegesterServiceContext _context;
        public AdminPanelService(RegesterServiceContext context)
        {
            _context = context;
        }
        public async Task<ResponseModel> Register(RegistrationModel registrationModel)
        {
            var existingAdmin = await _context.AdminInfos.FirstOrDefaultAsync(x => x.Email == registrationModel.Email);
            if (existingAdmin != null)
            {
                return new ResponseModel
                {
                    Success = false,
                    Message = "Admin with this email already exists."
                };
            }
        }
    }
}

using EntityLayer.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using VpnWeb.Models;

namespace BusiniessLayer.Abstract
{
    public interface IUserService
    {
        Task RegisterAsync(RegisterDto dto);
        Task<string> LoginAsync(LoginDto dto);
        Task<User> GetUserByIdAsync(Guid id);
        Task VerifyEmailAsync(string token);
        Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto);
        Task ForgotPasswordAsync(string email);
        Task ResetPasswordAsync(ResetPasswordDto dto);

    }




    public class LoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }


    public class RegisterDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string FullName { get; set; }
    }

    public class ResetPasswordDto
    {
        public string Token { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }

}

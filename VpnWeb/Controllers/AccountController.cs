using BusiniessLayer.Abstract;
using BusiniessLayer.Security;
using DataAcsessLayer.Concrete.Context;
using EntityLayer.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VpnWeb.Models;
using LoginDto = BusiniessLayer.Abstract.LoginDto;
using RegisterDto = BusiniessLayer.Abstract.RegisterDto;
//using VpnWeb.Models;

namespace VpnWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserService _userService;

        public AccountController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            await _userService.RegisterAsync(dto);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var token = await _userService.LoginAsync(dto);
            Response.Cookies.Append("access_token", token);
            return RedirectToAction("Index", "Connect");
        }


        [Authorize] // Sadece giriş yapmışlar girebilsin
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            // Giriş yapan kullanıcının ID'sini Cookie/Claim'den çekiyoruz
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

            // Bilgileri getir
            var user = await _userService.GetUserByIdAsync(userId);

            return View(user);
        }


        [HttpGet]  // Buraya güzel bi sayfa yapılacak ileride çalışıyor
        public async Task<IActionResult> VerifyEmail(string token)
        {
            try
            {
                await _userService.VerifyEmailAsync(token);
                return Ok("Hesabınız başarıyla doğrulandı! Artık giriş yapabilirsiniz.");
                // Veya: return Redirect("https://seninsiten.com/login?verified=true");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }



        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            try
            {
                // 1. Giriş yapan kullanıcının ID'sini al
                var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

                // 2. Servise gönder (Bütün işi UserService yapacak)
                await _userService.ChangePasswordAsync(userId, dto);

                // 3. Başarılı mesajı
                TempData["Success"] = "Şifreniz başarıyla güncellendi!";
            }
            catch (Exception ex)
            {
                // 4. Hata mesajı (Şifre yanlışsa vs. buraya düşecek)
                TempData["Error"] = ex.Message;
            }

            // Her durumda Profil sayfasına geri dön
            return RedirectToAction("Profile");
        }


        [HttpPost] // View tarafında form ile post ettiğimiz için HttpPost olmalı
        public IActionResult Logout()
        {
            // 1. Tarayıcıdaki token çerezini siliyoruz
            Response.Cookies.Delete("access_token");

            // 2. İsteğe bağlı: Kullanıcıya mesaj göster
            TempData["Success"] = "Başarıyla çıkış yapıldı.";

            // 3. Giriş sayfasına yönlendir
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            await _userService.ForgotPasswordAsync(email);
            return Ok("Eğer e-posta kayıtlıysa şifre sıfırlama linki gönderildi.");
        }

        [HttpGet]
        public IActionResult ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest("Geçersiz şifre sıfırlama bağlantısı.");

            return View(new ResetPasswordDto
            {
                Token = token
            });
        }


        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
            await _userService.ResetPasswordAsync(dto);
            return Ok("Şifreniz başarıyla güncellendi.");
        }


    }

} 

using BusiniessLayer.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace VpnWeb.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IUserService userService, ILogger<AuthController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                var token = await _userService.LoginAsync(dto);
                return Ok(new { token = token, message = "Giriş başarılı" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for user: {Email}", dto.Email);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            try
            {
                await _userService.RegisterAsync(dto);
                return Ok(new { message = "Kayıt başarılı! Lütfen e-postanızı doğrulayın." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration error for user: {Email}", dto.Email);
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}

using BusiniessLayer.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace VpnWeb.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        public UsersController(IUserService userService) => _userService = userService;

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();

            var user = await _userService.GetUserByIdAsync(Guid.Parse(userIdClaim.Value));
            if (user == null) return NotFound();

            return Ok(new
            {
                user.FullName,
                user.Email,
                user.CreatedAt,
                user.IsActive
            });
        }
    }
}

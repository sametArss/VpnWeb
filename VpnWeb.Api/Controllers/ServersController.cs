using BusiniessLayer.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VpnWeb.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Bu controller altındaki her şey giriş gerektirsin
    public class ServersController : ControllerBase
    {
        private readonly IVpnServerService _vpnServerService;
        private readonly ILogger<ServersController> _logger;

        public ServersController(IVpnServerService vpnServerService, ILogger<ServersController> logger)
        {
            _vpnServerService = vpnServerService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveServers()
        {
            try
            {
                var servers = await _vpnServerService.GetActiveServersAsync();
                return Ok(servers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active servers");
                return StatusCode(500, new { message = "Sunucular alınırken bir hata oluştu." });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetServerById(int id)
        {
            var server = await _vpnServerService.GetByIdAsync(id);
            if (server == null) return NotFound(new { message = "Sunucu bulunamadı." });
            return Ok(server);
        }
    }
}

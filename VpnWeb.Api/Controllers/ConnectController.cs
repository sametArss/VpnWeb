using BusiniessLayer.Abstract;
using EntityLayer.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace VpnWeb.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ConnectController : ControllerBase
    {
        private readonly IVpnServerService _vpnService;
        private readonly IUserVpnService _userVpnService;
        private readonly ILogger<ConnectController> _logger;

        public ConnectController(
            IVpnServerService vpnService,
            IUserVpnService userVpnService,
            ILogger<ConnectController> logger)
        {
            _vpnService = vpnService;
            _userVpnService = userVpnService;
            _logger = logger;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

        [HttpGet("best-server")]
        public async Task<IActionResult> GetBestServer()
        {
            var servers = await _vpnService.GetActiveServersAsync();
            if (!servers.Any()) return NotFound("Aktif VPN sunucusu bulunamadı.");

            // En düşük yük ve en düşük gecikmeye sahip sunucuyu seç
            var bestServer = servers
                .OrderBy(s => s.LoadPercent)
                .ThenBy(s => s.LatencyMs)
                .FirstOrDefault();

            return Ok(new
            {
                bestServer.Id,
                bestServer.Name,
                bestServer.Country,
                bestServer.IpAddress,
                bestServer.LoadPercent,
                bestServer.LatencyMs
            });
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var activeVpn = await _userVpnService.GetActiveVpnAsync(CurrentUserId);
            if (activeVpn == null) return Ok(new { isConnected = false });

            return Ok(new
            {
                isConnected = true,
                activeVpn.VpnServer.Name,
                activeVpn.VpnServer.Country,
                activeVpn.VpnServer.IpAddress,
                activeVpn.ClientIp,
                activeVpn.Protocol,
                activeVpn.ConnectedAt,
                activeVpn.ClientConfig, // 🔥 Config eklendi
                durationSeconds = (DateTime.UtcNow - activeVpn.ConnectedAt).TotalSeconds
            });
        }

        [HttpGet("config")]
        public async Task<IActionResult> GetConfig()
        {
            var activeVpn = await _userVpnService.GetActiveVpnAsync(CurrentUserId);
            if (activeVpn == null || string.IsNullOrEmpty(activeVpn.ClientConfig)) 
                return NotFound("Aktif config bulunamadı.");

            return Ok(new { config = activeVpn.ClientConfig, protocol = activeVpn.Protocol });
        }

        [HttpPost("connect")]
        public async Task<IActionResult> Connect([FromBody] ConnectRequest request)
        {
            try
            {
                // Eğer sunucu ID belirtilmemişse en iyisini seç
                int serverId = request.ServerId;
                if (serverId <= 0)
                {
                    var servers = await _vpnService.GetActiveServersAsync();
                    var best = servers.OrderBy(s => s.LoadPercent).ThenBy(s => s.LatencyMs).FirstOrDefault();
                    if (best == null) return BadRequest("Bağlanılacak uygun sunucu bulunamadı.");
                    serverId = best.Id;
                }

                await _userVpnService.ConnectUserToVpnAsync(CurrentUserId, serverId, request.Protocol);
                
                var status = await _userVpnService.GetActiveVpnAsync(CurrentUserId);
                return Ok(new { 
                    message = "Bağlantı başarılı",
                    ipAddress = status.VpnServer.IpAddress,
                    protocol = (int)status.Protocol,
                    clientConfig = status.ClientConfig, // 🔥 Config buraya eklendi
                    connectedAt = status.ConnectedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VPN bağlantı hatası");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("disconnect")]
        public async Task<IActionResult> Disconnect()
        {
            try
            {
                await _userVpnService.DisconnectAsync(CurrentUserId);
                return Ok(new { message = "Bağlantı kesildi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VPN bağlantı kesme hatası");
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class ConnectRequest
    {
        public int ServerId { get; set; }
        public VpnProtocol Protocol { get; set; } = VpnProtocol.OpenVPN;
    }
}

using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Threading.Tasks;
using TelephoneID.Services;

[ApiController]
public class StreamController : ControllerBase
{
    private readonly CallStreamService _callStreamService;
    public StreamController(CallStreamService callStreamService)
    {
        _callStreamService = callStreamService;
    }

    // Endpoint to accept Twilio's WebSocket connection
    [Route("/stream")]
    public async Task<IActionResult> HandleCallStream()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            // Accept the incoming WebSocket handshake from Twilio
            WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            // Hand off the socket to our service for processing audio and events
            await _callStreamService.ProcessCallAsync(webSocket);
            return new EmptyResult();  // Socket processing done
        }
        else
        {
            // If an HTTP request hits this endpoint without upgrading to WebSocket
            return BadRequest("Expected WebSocket connection.");
        }
    }
}

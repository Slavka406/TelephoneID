using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
// using Twilio.TwiML;  // (Optional: Twilio helper library for generating TwiML)

[ApiController]
[Route("api/[controller]")]
public class TwilioCallController : ControllerBase
{
    private readonly IConfiguration _config;
    public TwilioCallController(IConfiguration config)
    {
        _config = config;
    }

    // Twilio will make an HTTP request here when a call arrives (Voice webhook).
    [HttpPost("voice")]  // e.g. POST /api/twiliocall/voice
    public IActionResult HandleIncomingCall()
    {
        // Twilio sends call info as form data (we can capture if needed)
        string callSid = Request.Form["CallSid"];
        string fromNumber = Request.Form["From"];
        string toNumber = Request.Form["To"];
        // (We might log these or use them for any custom logic; not needed just to stream audio.)

        // Construct the WebSocket URL that Twilio should connect to.
        // It must be "wss" (secure WebSocket) as required by Twilio【48†L492-L499】, pointing to our stream endpoint.
        string host = Request.Host.Value;
        string wsEndpoint = $"wss://{host}/stream";  // WebSocket path for Twilio to stream audio

        // Generate TwiML response instructing Twilio to <Connect> the call audio to our WebSocket stream
        string twimlResponse = 
            $"<Response><Connect><Stream url='{wsEndpoint}' /></Connect></Response>";

        // Twilio expects the TwiML XML in the HTTP response with content type text/xml
        return Content(twimlResponse, "text/xml");
    }
}

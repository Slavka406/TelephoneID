using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

public class TwilioCallService
{
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _warningMessage;

    public TwilioCallService(IConfiguration config)
    {
        _accountSid = config["Twilio:AccountSID"];
        _authToken = config["Twilio:AuthToken"];
        _warningMessage = "This call has been identified as fraudulent. It will now be disconnected.";
    }

    public void PlayWarningAndHangUp(string callSid)
    {
        if (string.IsNullOrEmpty(callSid)) return;
        // Initialize Twilio client with credentials
        TwilioClient.Init(_accountSid, _authToken);

        // Prepare TwiML to speak the warning then hang up
        string twiml = $"<Response><Say voice='alice'>{_warningMessage}</Say><Hangup/></Response>";
        try
        {
            // Update the live call with the TwiML (Twilio will execute this immediately on the call)【12†L217-L224】
            CallResource.Update(pathSid: callSid, twiml: new Twiml(twiml));
            // After this, Twilio will play the message to the caller and end the call.
        }
        catch (Exception ex)
        {
            // Log or handle error (e.g., network issue, invalid SID)
        }
    }
}

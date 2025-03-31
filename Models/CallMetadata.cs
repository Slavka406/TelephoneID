namespace TelephoneID.Models;

public class CallMetadata
{
    public string CallSid { get; set; }
    public string From { get; set; }
    public string To { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}
namespace TelephoneID.Models;

public class TranscriptSegment
{
    public TimeSpan Offset { get; set; }   // time from start of call when this segment was spoken
    public string Text { get; set; }
}
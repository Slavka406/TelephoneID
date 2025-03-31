using System;
using System.Collections.Concurrent;
using System.IO;
using Azure.Storage.Blobs;
using TelephoneID.Models;
using TelephoneID.Helpers;

public class StorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _audioContainerName = "call-audio";
    private readonly string _logContainerName = "call-logs";

    // Buffer to accumulate audio bytes for each call (keyed by CallSid)
    private ConcurrentDictionary<string, MemoryStream> _audioBuffers = new();

    public StorageService(IConfiguration config)
    {
        string blobConnectionString = config["Azure:BlobConnectionString"];
        _blobServiceClient = new BlobServiceClient(blobConnectionString);
        // Ensure containers exist (creates them if not already present)
        _blobServiceClient.CreateBlobContainer(_audioContainerName);
        _blobServiceClient.CreateBlobContainer(_logContainerName);
    }

    public void InitAudioRecording(string callSid)
    {
        // Create a new MemoryStream buffer for this call's raw audio
        _audioBuffers[callSid] = new MemoryStream();
    }

    public void AppendAudioChunk(string callSid, byte[] chunk)
    {
        if (_audioBuffers.TryGetValue(callSid, out MemoryStream stream))
        {
            stream.Write(chunk, 0, chunk.Length);
        }
    }

    public string FinalizeAudioRecording(string callSid)
    {
        if (!_audioBuffers.TryGetValue(callSid, out MemoryStream rawAudio))
            return null;
        try
        {
            rawAudio.Seek(0, SeekOrigin.Begin);
            // Convert raw PCM audio to WAV format (including WAV header)
            MemoryStream wavStream = ConvertRawToWav(rawAudio);
            wavStream.Seek(0, SeekOrigin.Begin);

            // Upload the WAV stream to Azure Blob Storage
            string fileName = $"{callSid}.wav";
            BlobContainerClient audioContainer = _blobServiceClient.GetBlobContainerClient(_audioContainerName);
            BlobClient audioBlob = audioContainer.GetBlobClient(fileName);
            audioBlob.Upload(wavStream, overwrite: true);
            return audioBlob.Uri.ToString();  // return URL of the audio file in blob
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., conversion or upload failure)
            return null;
        }
        finally
        {
            // Clean up the in-memory buffer
            rawAudio.Dispose();
            _audioBuffers.TryRemove(callSid, out _);
        }
    }

    public void SaveCallTranscriptLog(CallMetadata callInfo, string transcript, FraudCheckResult fraudResult)
    {
        // Create an object with all info
        var logObject = new
        {
            callSid = callInfo.CallSid,
            from = callInfo.From,
            to = callInfo.To,
            startTime = callInfo.StartTime,
            endTime = callInfo.EndTime,
            transcript = transcript,
            fraudDetected = fraudResult.IsFraud,
            fraudReason = fraudResult.Reason
        };
        string logJson = System.Text.Json.JsonSerializer.Serialize(logObject);

        // Upload the JSON log to blob storage
        string logFileName = $"{callInfo.CallSid}.json";
        BlobContainerClient logContainer = _blobServiceClient.GetBlobContainerClient(_logContainerName);
        BlobClient logBlob = logContainer.GetBlobClient(logFileName);
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(logJson));
        logBlob.Upload(ms, overwrite: true);
    }

    private MemoryStream ConvertRawToWav(Stream rawPcmStream)
    {
        // Convert raw PCM (8-bit μ-law 8kHz from Twilio) to WAV PCM 16-bit 8kHz.
        // (For brevity, this shows a simple approach; a robust solution might use a library like NAudio.)
        rawPcmStream.Seek(0, SeekOrigin.Begin);
        MemoryStream wavStream = new MemoryStream();
        using (BinaryWriter writer = new BinaryWriter(wavStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            int sampleRate = 8000;
            short channels = 1;
            short bitsPerSample = 16;
            // Calculate derived values
            int byteRate = sampleRate * channels * (bitsPerSample / 8);
            short blockAlign = (short)(channels * (bitsPerSample / 8));
            // Convert μ-law to PCM samples (Twilio sends μ-law, we need PCM for WAV)
            // μ-law decoding: each byte -> 16-bit sample. (This is a simplified placeholder.)
            MemoryStream pcmStream = MuLawDecode(rawPcmStream);

            // Write WAV header
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + pcmStream.Length);            // File size minus 8 bytes
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);                               // PCM format chunk length
            writer.Write((short)1);                         // format tag 1 = PCM
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write((int)pcmStream.Length);

            // Write PCM data
            pcmStream.Seek(0, SeekOrigin.Begin);
            pcmStream.CopyTo(writer.BaseStream);
        }
        wavStream.Seek(0, SeekOrigin.Begin);
        return wavStream;
    }

    private MemoryStream MuLawDecode(Stream muLawStream)
    {
        // Dummy μ-law decoding: in practice, convert each byte from μ-law to linear PCM 16-bit.
        MemoryStream pcmStream = new MemoryStream();
        int b;
        // Simple approach: Twilio μ-law is 8-bit, we use a lookup or algorithm to get short.
        // Here we assume identity (not correct for real audio, but placeholder).
        while ((b = muLawStream.ReadByte()) != -1)
        {
            short sample = MuLawDecoder.MuLawToLinearSample((byte)b);  // decode μ-law to PCM sample
            byte lowByte = (byte)(sample & 0xFF);
            byte highByte = (byte)((sample >> 8) & 0xFF);
            pcmStream.WriteByte(lowByte);
            pcmStream.WriteByte(highByte);
        }
        pcmStream.Seek(0, SeekOrigin.Begin);
        return pcmStream;
    }
}

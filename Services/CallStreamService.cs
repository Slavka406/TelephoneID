using System;
using System.Text;
using System.Text.Json;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using TelephoneID.Models;

namespace TelephoneID.Services
{
  public class CallStreamService
  {
      private readonly TranscriptionService _transcriptionService;
      private readonly FraudDetectionService _fraudService;
      private readonly TwilioCallService _twilioCallService;
      private readonly StorageService _storageService;
      // You could also inject an ILogger<CallStreamService> for logging (omitted for brevity).

      public CallStreamService(TranscriptionService transcriptionService,
                                FraudDetectionService fraudService,
                                TwilioCallService twilioCallService,
                                StorageService storageService)
      {
          _transcriptionService = transcriptionService;
          _fraudService = fraudService;
          _twilioCallService = twilioCallService;
          _storageService = storageService;
      }

      public async Task ProcessCallAsync(WebSocket socket)
      {
          // Data structures to hold call state:
          CallMetadata callInfo = new CallMetadata();
          StringBuilder fullTranscript = new StringBuilder();
          bool fraudFlagged = false;
          string fraudReason = null;

          // Start the transcription service for this call (e.g., open stream to Deepgram or init Azure SDK)
          _transcriptionService.StartTranscriptionStream();

          // Set up a periodic task for fraud checks every 20 seconds
          CancellationTokenSource cts = new CancellationTokenSource();
          Task fraudCheckTask = Task.Run(async () =>
          {
              while (!cts.Token.IsCancellationRequested)
              {
                  await Task.Delay(TimeSpan.FromSeconds(20), cts.Token);
                  string transcriptSoFar = fullTranscript.ToString();
                  if (!string.IsNullOrEmpty(transcriptSoFar))
                  {
                      bool isFraud = _fraudService.AnalyzeTranscript(transcriptSoFar, out string reason);
                      if (isFraud && !fraudFlagged)
                      {
                          // Fraud detected – mark it and trigger Twilio call termination
                          fraudFlagged = true;
                          fraudReason = reason;
                          _twilioCallService.PlayWarningAndHangUp(callInfo.CallSid);
                          break;  // break out of the fraud check loop after initiating hang-up
                      }
                  }
              }
          }, cts.Token);

          var buffer = new byte[4096];
          WebSocketReceiveResult result;
          try
          {
              // Continuously receive messages from Twilio until the socket closes
              while ((result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None)).CloseStatus == null)
              {
                  string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                  using JsonDocument doc = JsonDocument.Parse(json);
                  string eventType = doc.RootElement.GetProperty("event").GetString();

                  switch (eventType)
                  {
                      case "connected":
                          // Twilio WebSocket connected event (no payload besides streamSid usually). 
                          // We could log this if needed.
                          break;

                      case "start":
                          // Call streaming start event – contains call and stream metadata.
                          JsonElement startPayload = doc.RootElement.GetProperty("start");
                          callInfo.CallSid = startPayload.GetProperty("callSid").GetString();
                          callInfo.From = startPayload.GetProperty("from").GetString();
                          callInfo.To = startPayload.GetProperty("to").GetString();
                          callInfo.StartTime = DateTime.UtcNow;
                          // Initialize audio recording in storage (prepare to save audio data)
                          _storageService.InitAudioRecording(callInfo.CallSid);
                          // (Optional: log call start, e.g., _logger.LogInformation($"Call {callInfo.CallSid} started streaming."); )
                          break;

                      case "media":
                          // Incoming audio chunk (raw audio encoded as Base64 string)
                          string base64Audio = doc.RootElement.GetProperty("media").GetProperty("payload").GetString();
                          byte[] audioChunk = Convert.FromBase64String(base64Audio);
                          // Save the audio chunk to blob storage (or local buffer)
                          _storageService.AppendAudioChunk(callInfo.CallSid, audioChunk);
                          // Send the audio chunk to the transcription service for real-time transcription
                          string partialText = _transcriptionService.SendAudioChunk(audioChunk);
                          if (!string.IsNullOrEmpty(partialText))
                          {
                              // Append any transcribed text to our running transcript
                              fullTranscript.Append(partialText);
                              // (For more advanced use, we might handle partial vs final transcripts differently)
                          }
                          break;

                      case "stop":
                          // Twilio indicates the call (media stream) has ended
                          callInfo.EndTime = DateTime.UtcNow;
                          cts.Cancel();  // stop the fraud check task
                          // Finalize transcription (there might be some buffered words)
                          string finalText = _transcriptionService.StopTranscriptionStream();
                          if (!string.IsNullOrEmpty(finalText))
                          {
                              fullTranscript.Append(finalText);
                          }
                          // Close the WebSocket as we have no more data to receive
                          await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Call ended", CancellationToken.None);
                          break;
                  }
              }
          }
          catch (Exception ex)
          {
              // Handle any exceptions in receiving/parsing (network errors, JSON errors, etc.)
              // _logger.LogError(ex, "Error during WebSocket processing for Call SID: {CallSid}", callInfo.CallSid);
          }
          finally
          {
              // Clean up regardless of normal closure or error
              cts.Cancel();  // ensure the fraud check loop stops
              // Finalize and save the audio recording to Azure Blob (as .wav)
              string audioUrl = _storageService.FinalizeAudioRecording(callInfo.CallSid);
              // Compile the full transcript text
              string transcriptText = fullTranscript.ToString();
              // Save the call log (metadata, transcript, fraud result) to Azure Blob as JSON
              var fraudResult = new FraudCheckResult { IsFraud = fraudFlagged, Reason = fraudReason };
              _storageService.SaveCallTranscriptLog(callInfo, transcriptText, fraudResult);
              // (Optional: log call end and results, e.g., _logger.LogInformation($"Call {callInfo.CallSid} ended. FraudDetected={fraudFlagged}"); )
          }
      }
  }
}
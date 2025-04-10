using Microsoft.Extensions.Options;
using System.Diagnostics;
using UniviewNvrApi.Models;

namespace UniviewNvrApi.Services
{
    public class RtspStreamingService : IHostedService, IDisposable
    {
        private readonly RtspSettings _settings;
        private readonly ILogger<RtspStreamingService> _logger;
        private readonly Dictionary<int, Process> _ffmpegProcesses = new();

        public RtspStreamingService(IOptions<RtspSettings> settings, ILogger<RtspStreamingService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Create HLS directory if it doesn't exist
            string hlsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/hls");
            Directory.CreateDirectory(hlsDir);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var process in _ffmpegProcesses.Values)
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.Dispose();
                }
            }
            _ffmpegProcesses.Clear();
            _logger.LogInformation("Stopped all FFmpeg processes");
            return Task.CompletedTask;
        }

        public void StartStream(int channel, int streamType = 0)
        {
            if (_ffmpegProcesses.ContainsKey(channel))
            {
                _logger.LogWarning("Stream for channel {Channel} is already running", channel);
                return;
            }

            string rtspUrl = $"rtsp://{_settings.Username}:{_settings.Password}@{_settings.NvrIp}:{_settings.Port}/unicast/c{channel}/s{streamType}/live";

        //rtsp://41145:Sonali%4041145@10.98.142.250:8554/unicast/c1/s0/live
        //rtsp://41145:Sonali%4041145@10.98.142.250:8554/unicast/c2/s0/live

            string hlsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot\\hls");
            string hlsOutput = Path.Combine(hlsDir, $"stream_channel{channel}.m3u8");

            if (!File.Exists(_settings.FfmpegPath))
            {
                _logger.LogError("FFmpeg not found at {Path}", _settings.FfmpegPath);
                throw new FileNotFoundException("FFmpeg executable not found.", _settings.FfmpegPath);
            }


            //_logger.LogError("FFmpeg not found at {Path}", _settings.FfmpegPath);
            _logger.LogInformation(_settings.FfmpegPath+ $" -re -loglevel debug -rtsp_transport tcp -probesize 50000000 -analyzeduration 50000000 -fflags +genpts -i \"{rtspUrl}\" -c:v libx264 -preset ultrafast -force_key_frames \"expr:gte(t,n_forced*2)\" -c:a aac -f hls -hls_time 5 -hls_list_size 10 -hls_segment_filename \"{Path.Combine(hlsDir, $"segment_channel{channel}_%d.ts")}\" \"{hlsOutput}\"");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _settings.FfmpegPath,
                    Arguments = $" -re -loglevel debug -rtsp_transport tcp  -probesize 100000000 -analyzeduration 100000000 -fflags +genpts -i \"{rtspUrl}\" -c:v libx264 -preset ultrafast -force_key_frames \"expr:gte(t,n_forced*2)\" -an -f hls -hls_time 5 -hls_list_size 0 -hls_segment_filename \"{Path.Combine(hlsDir, $"segment_channel{channel}_%d.ts")}\" \"{hlsOutput}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (s, e) => 
            { 
                if (e.Data != null) _logger.LogInformation("FFmpeg OutputDataReceived [Channel {Channel}]: {Data}", channel, e.Data); 
            
            };
            process.ErrorDataReceived += (s, e) => 
            { 
               _logger.LogError("FFmpeg ErrorDataReceived [Channel {Channel}]: {Data}", channel, e.Data); 
            };
            process.Exited += (s, e) =>
            {
                _logger.LogError("FFmpeg Exited {Channel} exited with code {ExitCode}", channel, process.ExitCode);
                _ffmpegProcesses.Remove(channel);
            };

            bool started = process.Start();
            if (!started)
            {
                _logger.LogError("Failed to start FFmpeg for channel {Channel}", channel);
                throw new InvalidOperationException("FFmpeg process failed to start.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _ffmpegProcesses[channel] = process;
            _logger.LogInformation("Started streaming for channel {Channel}", channel);
        }

        public void StopStream(int channel)
        {
            if (!_ffmpegProcesses.ContainsKey(channel))
            {
                _logger.LogWarning("No stream running for channel {Channel} to stop", channel);
                return;
            }

            var process = _ffmpegProcesses[channel];
            if (!process.HasExited)
            {
                process.Kill(); // Forcefully stop FFmpeg
                process.WaitForExit(5000); // Wait up to 5s for clean exit
            }

            _ffmpegProcesses.Remove(channel);
            _logger.LogInformation("Stopped FFmpeg process for channel {Channel}", channel);

            // Clean up HLS files
            string hlsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/hls");
            string hlsOutput = Path.Combine(hlsDir, $"stream_channel{channel}.m3u8");
            string segmentPattern = Path.Combine(hlsDir, $"segment_channel{channel}_*.ts");

            try
            {
                if (File.Exists(hlsOutput))
                {
                    File.Delete(hlsOutput);
                    _logger.LogInformation("Deleted {HlsOutput}", hlsOutput);
                }

                foreach (var segment in Directory.GetFiles(hlsDir, $"segment_channel{channel}_*.ts"))
                {
                    File.Delete(segment);
                    _logger.LogInformation("Deleted {Segment}", segment);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to clean up HLS files for channel {Channel}: {Error}", channel, ex.Message);
            }

        }

        public bool IsStreaming(int channel) => _ffmpegProcesses.ContainsKey(channel);

        public void Dispose() => StopAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}

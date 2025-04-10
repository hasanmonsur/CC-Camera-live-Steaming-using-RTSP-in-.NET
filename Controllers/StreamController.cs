using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UniviewNvrApi.Services;

namespace UniviewNvrApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StreamController : ControllerBase
    {
        private readonly RtspStreamingService _streamingService;

        public StreamController(RtspStreamingService streamingService)
        {
            _streamingService = streamingService;
        }

        [HttpGet("start/{channel}")]
        public IActionResult StartStream(int channel, [FromQuery] int streamType = 0)
        {
            try
            {
                _streamingService.StartStream(channel, streamType);
                return Ok(new { Message = $"Started streaming channel {channel}", HlsUrl = $"/hls/stream_channel{channel}.m3u8" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Failed to start stream", Details = ex.Message });
            }
        }

        [HttpGet("stop/{channel}")]
        public IActionResult StopStream(int channel)
        {
            _streamingService.StopStream(channel);
            return Ok(new { Message = $"Stopped streaming channel {channel}" });
        }

        [HttpGet("status/{channel}")]
        public IActionResult GetStreamStatus(int channel)
        {
            bool isStreaming = _streamingService.IsStreaming(channel);
            return Ok(new { Channel = channel, IsStreaming = isStreaming, HlsUrl = isStreaming ? $"/hls/stream_channel{channel}.m3u8" : null });
        }
    }
}

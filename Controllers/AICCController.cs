using AICCServer.Models;
using AICCServer.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AICCServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AICCController : Controller
    {
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AICCSettings _aiccSettings;
        private readonly ILogger<AICCController> _logger;

        public AICCController(IMemoryCache cache, IHttpClientFactory httpClientFactory, IOptions<AICCSettings> aiccOptions, ILogger<AICCController> logger)
        {
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _aiccSettings = aiccOptions.Value;
            _logger = logger;
        }

        [AcceptVerbs("GET", "HEAD")]
        [Route("InitialiseRelay")]
        public ActionResult InitialiseRelay([FromQuery] string? AICC_SID, [FromQuery] string? AICC_URL, [FromQuery] string? CMI, string CONTENT_URL)
        {
            //Check for ping from Moodle
            if (string.IsNullOrEmpty(AICC_SID) && string.IsNullOrEmpty(AICC_URL))
            {
                return Ok();
            }
            _logger.LogDebug("AICC_SID: {AICC_SID}, AICC_URL {AICC_URL}, CMI: {CMI}, CONTENT_URL: {CONTENT_URL}", AICC_SID, AICC_URL, CMI, CONTENT_URL);
            var relayLogId = Guid.NewGuid();
            _cache.Set($"aicc::relaysession::{relayLogId}",
                       AICC_URL);
            var sessionPageUrl = String.Empty;
            using (_logger.BeginScope(new Dictionary<string, object> { ["SessionId"] = relayLogId }))
            {
                _logger.LogDebug("AICC_SID: {AICC_SID}, AICC_URL {AICC_URL}, CMI: {CMI}, CONTENT_URL: {CONTENT_URL}", AICC_SID, AICC_URL, CMI, CONTENT_URL);
                string strRelayPageUrl = $"https://{Request.Host.Value}/AICC/Relay/{relayLogId}";
               
                string urlBase = _aiccSettings.BaseUrl;

                sessionPageUrl = QueryHelpers.AddQueryString(
                    $"{urlBase}{CONTENT_URL}",
                    new Dictionary<string, string?>
                    {
                        ["AICC_URL"] = strRelayPageUrl,
                        ["AICC_SID"] = AICC_SID
                    });
            }
            return Redirect(sessionPageUrl);
        }

        [HttpPost("Relay/{id}")]
        public async Task<IActionResult> Relay(Guid id)
        {
            var formData = Request.Form
                        .ToDictionary(k => k.Key, v => v.Value.ToString());
            string? result;
            using (_logger.BeginScope(new Dictionary<string, object> { ["sessionId"] = formData["session_id"] }))
            {
                try
                {
                    _logger.LogDebug("Relay Request form: {Request_form}", Request.Form.ToString());
                    var cacheKey = "aicc::relaysession::" + id;

                    var lmsAICCUrl = (string?)_cache.Get(cacheKey);

                    if (string.IsNullOrEmpty(lmsAICCUrl))
                        return BadRequest("Invalid or missing LMS URL");

                    //Create HttpClient from the factory
                    var client = _httpClientFactory.CreateClient();

                    if (formData["command"].ToLower() == "exitparam")
                    {
                        formData["command"] = "PutParam";
                        var parsed = AiccDataParser.Parse(formData["aicc_data"]);
                        _logger.LogInformation("AICC PutParam received: {@AiccDetails}", new AiccLogEntry(
                           SessionId: formData["session_id"],
                           RequestType: formData["command"],
                           CourseId: parsed.CourseId,
                           StudentId: parsed.StudentId,
                           LessonStatus: parsed.LessonStatus,
                           Score: parsed.Score,
                           Time: parsed.Time,
                           LessonLocation: parsed.LessonLocation
                        ));
                        var content = new FormUrlEncodedContent(formData);
                        var response = await client.PostAsync(lmsAICCUrl, content);
                        result = await response.Content.ReadAsStringAsync();

                        formData["command"] = "ExitAU";
                        formData.Remove("AICC_DATA");
                        formData.Remove("aicc_data");
                        _logger.LogInformation("AICC ExitAU received: {@AiccDetails}", new AiccLogEntry(
                            SessionId: formData["session_id"],
                            RequestType: formData["command"]
                        ));
                        content = new FormUrlEncodedContent(formData);
                        response = await client.PostAsync(lmsAICCUrl, content);
                        result = await response.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        //pass through as usual
                        if (formData["command"] == "PutParam")
                        {
                            var parsed = AiccDataParser.Parse(formData["aicc_data"]);
                            _logger.LogInformation("AICC PutParam received: {@AiccDetails}", new AiccLogEntry(
                               SessionId: formData["session_id"],
                               RequestType: formData["command"],
                               CourseId: parsed.CourseId,
                               StudentId: parsed.StudentId,
                               LessonStatus: parsed.LessonStatus,
                               Score: parsed.Score,
                               Time: parsed.Time,
                               LessonLocation: parsed.LessonLocation
                            ));
                        }
                        var content = new FormUrlEncodedContent(formData);
                        var response = await client.PostAsync(lmsAICCUrl, content);
                        result = await response.Content.ReadAsStringAsync();
                        if (formData["command"] == "GetParam")
                        {
                            var parsed = AiccDataParser.Parse(result);
                            _logger.LogInformation("AICC GetParam result: {@AiccDetails}", new AiccLogEntry(
                               SessionId: formData["session_id"],
                               RequestType: formData["command"],
                               CourseId: parsed.CourseId,
                               StudentId: parsed.StudentId,
                               LessonStatus: parsed.LessonStatus,
                               Score: parsed.Score,
                               Time: parsed.Time,
                               LessonLocation: parsed.LessonLocation
                           ));
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError("Error Occurred: {Message}", e.Message);
                    throw;
                }
                result = DoAbInitioInterception(formData, result);

                return Ok(result);
            }
        }

        private string? DoAbInitioInterception(Dictionary<string, string> formData, string? result)
        {
            if (formData["command"].ToLower() != "getparam")
                return result;

            if (_aiccSettings.AICCRelayAbInitioIntercept != "on")
                return result;

            var variantList = _aiccSettings.AICCRelayAbInitioInterceptList;
            if (string.IsNullOrEmpty(variantList))
                return result;

            foreach (var variant in variantList)
            {
                if (!result.ToLower().Contains("lesson_status=" + variant))
                    break;

                var abInitPosition = result.IndexOf(variant);
                var lessonStatusKey = result.Substring(abInitPosition - 14, 14);
                //check if we have found the right key
                if (lessonStatusKey.ToLower() == "lesson_status=")
                {
                    result = result.Replace(lessonStatusKey + variant, lessonStatusKey + "not attempted, ab-initio");
                }
            }
            return result;
        }
    }
}
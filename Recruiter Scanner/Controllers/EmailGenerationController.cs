using Microsoft.AspNetCore.Mvc;
using Recruiter_Scanner.Models;
using Recruiter_Scanner.Services;
using System.Text.Json;

namespace Recruiter_Scanner.Controllers
{
    public class EmailGenerationController : Controller
    {
        // Used when the client doesn't send a CV.
        private const string FallbackCv = @"Marko Keser - Backend Developer
Location: Barcelona, Spain

Experience:
- Backend/Math Developer at Wicked Games (Feb 2025 - Oct 2025)
- .NET Developer at Quadro Consulting (Jan 2024 - Jan 2025)
- Intern at AG4.0 (Sep 2023 - Dec 2023)

Technical Skills:
- C#, ASP.NET Core, .NET 8+, Entity Framework
- SQL Server, MySQL, Complex SQL queries, optimization
- REST APIs, Authentication (RBAC, claims)
- Microsoft Azure basics, Railway deployment
- OpenAI API integrations

Projects:
- Restaurant Management System (production app with real-time data)
- Live: zubac-matine-production.up.railway.app

GitHub: github.com/markokeser
Certifications: English C1, C# Advanced, Salesforce Developer I";

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly IEmailGenerationService _emailService;
        private readonly ILogger<EmailGenerationController> _logger;

        public EmailGenerationController(
            IEmailGenerationService emailService,
            ILogger<EmailGenerationController> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        // GET: /EmailGeneration
        [HttpGet]
        public IActionResult Index()
        {
            return View(new EmailGeneratorViewModel());
        }

        // POST: /EmailGeneration/UploadJson
        [HttpPost]
        public IActionResult UploadJson(IFormFile jsonFile)
        {
            if (jsonFile == null || jsonFile.Length == 0)
            {
                TempData["Error"] = "Please choose a JSON file.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var document = JsonDocument.Parse(jsonFile.OpenReadStream());

                // Accept both an array of matches and a single match object.
                var matches = document.RootElement.ValueKind switch
                {
                    JsonValueKind.Array => document.RootElement.Deserialize<List<MatchData>>(JsonOptions),
                    JsonValueKind.Object => document.RootElement.Deserialize<MatchData>(JsonOptions) is { } single
                        ? new List<MatchData> { single }
                        : null,
                    _ => null
                };

                if (matches == null || matches.Count == 0)
                {
                    TempData["Error"] = "The JSON file doesn't contain any matches.";
                    return RedirectToAction(nameof(Index));
                }

                return View("Index", new EmailGeneratorViewModel { Matches = matches });
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid JSON uploaded: {FileName}", jsonFile.FileName);
                TempData["Error"] = $"Invalid JSON file: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /EmailGeneration/GenerateEmail
        [HttpPost]
        public Task<IActionResult> GenerateEmail([FromBody] GenerateEmailRequest request) =>
            GenerateMessage(request, _emailService.GenerateEmail);

        // POST: /EmailGeneration/GenerateLinkedInMessage
        [HttpPost]
        public Task<IActionResult> GenerateLinkedInMessage([FromBody] GenerateEmailRequest request) =>
            GenerateMessage(request, _emailService.GenerateLinkedInMessage);

        private async Task<IActionResult> GenerateMessage(
            GenerateEmailRequest request,
            Func<Recruiter, string, AIMatchResponse, Task<EmailContent>> generate)
        {
            if (request?.MatchData == null)
                return BadRequest(new { success = false, error = "Match data is required" });

            try
            {
                var cvData = string.IsNullOrWhiteSpace(request.CvData) ? FallbackCv : request.CvData;
                var content = await generate(ToRecruiter(request.MatchData), cvData, ToMatchResponse(request.MatchData));

                return Ok(new
                {
                    success = true,
                    subject = content.Subject,
                    body = content.Body,
                    tone = content.Tone,
                    keySellingPoints = content.KeySellingPoints
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Message generation failed for {Company}", request.MatchData.CompanyName);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        private static Recruiter ToRecruiter(MatchData match)
        {
            var nameParts = (match.RecruiterName ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return new Recruiter
            {
                FirstName = nameParts.ElementAtOrDefault(0) ?? "Recruiter",
                LastName = nameParts.ElementAtOrDefault(1) ?? string.Empty,
                Title = match.RecruiterTitle,
                CompanyNameForEmails = match.CompanyName,
                Website = match.Website,
                City = match.City,
                Country = match.Country,
                CompanyCity = match.City,
                EmailStatus = match.EmailStatus,
                Email = match.RecruiterEmail,
                PersonLinkedinUrl = match.RecruiterLinkedIn,
                CompanyLinkedinUrl = match.CompanyLinkedIn
            };
        }

        private static AIMatchResponse ToMatchResponse(MatchData match)
        {
            return new AIMatchResponse
            {
                Score = match.MatchScore ?? 0,
                Reasoning = match.Reasoning,
                CompanyAnalysis = match.CompanyAnalysis,
                LocationMatch = match.LocationMatch,
                IndustryMatch = match.IndustryMatch,
                KeyFindings = match.KeyFindings,
                Strengths = match.Strengths ?? new List<string>(),
                Weaknesses = match.Weaknesses ?? new List<string>()
            };
        }
    }

    public class GenerateEmailRequest
    {
        public MatchData MatchData { get; set; } = null!;
        public string? CvData { get; set; }
        public bool IncludeSignature { get; set; } = true;
    }
}

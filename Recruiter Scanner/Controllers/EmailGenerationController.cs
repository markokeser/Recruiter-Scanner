using Microsoft.AspNetCore.Mvc;
using Recruiter_Scanner.Services;
using Recruiter_Scanner.Models;
using System.Text.Json;

namespace Recruiter_Scanner.Controllers
{
    public class EmailGenerationController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;
        private readonly IEmailGenerationService _emailService;
        private readonly IAIService _aiService;
        private readonly ILogger<EmailGenerationController> _logger;

        public EmailGenerationController(
            IWebHostEnvironment env,
            IConfiguration configuration,
            IEmailGenerationService emailService,
            IAIService aiService,
            ILogger<EmailGenerationController> logger)
        {
            _env = env;
            _configuration = configuration;
            _emailService = emailService;
            _aiService = aiService;
            _logger = logger;
    }

        // GET: /EmailGeneration
        [HttpGet]
        public IActionResult Index()
        {
            var viewModel = new EmailGeneratorViewModel
            {
                Matches = new List<MatchData>()
            };
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> GenerateLinkedInMessage([FromBody] GenerateEmailRequest request)
        {
            try
            {
                if (request?.MatchData == null)
                    return BadRequest(new { error = "Match data is required" });

                // Učitaj CV (iz fajla ili baze)
                var cvData = await LoadCVData();

                // Konvertuj MatchData u AIMatchResponse
                var matchResponse = MapToAIMatchResponse(request.MatchData);

                // Kreiraj Recruiter objekat iz MatchData
                var recruiter = new Recruiter
                {
                    FirstName = request.MatchData.RecruiterName?.Split(' ').FirstOrDefault() ?? "Recruiter",
                    LastName = request.MatchData.RecruiterName?.Split(' ').Skip(1).FirstOrDefault() ?? "",
                    Title = request.MatchData.RecruiterTitle,
                    CompanyNameForEmails = request.MatchData.CompanyName,
                    Website = request.MatchData.Website,
                    City = request.MatchData.City,
                    Country = request.MatchData.Country,
                    CompanyCity = request.MatchData.City,
                    EmailStatus = request.MatchData.EmailStatus,
                    Email = request.MatchData.RecruiterEmail
                };

                // Generiši LinkedIn poruku preko servisa
                var linkedInMessage = await _emailService.GenerateLinkedInMessage(recruiter, cvData, matchResponse);

                return Ok(new
                {
                    success = true,
                    subject = linkedInMessage.Subject,
                    body = linkedInMessage.Body,
                    tone = linkedInMessage.Tone,
                    keySellingPoints = linkedInMessage.KeySellingPoints
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST: /EmailGeneration/UploadJson
        [HttpPost]
        public IActionResult UploadJson(IFormFile jsonFile)
        {
            try
            {
                if (jsonFile == null || jsonFile.Length == 0)
                {
                    TempData["Error"] = "Molim izaberite JSON fajl.";
                    return RedirectToAction(nameof(Index));
                }

                using var stream = new MemoryStream();
                jsonFile.CopyTo(stream);
                var jsonString = System.Text.Encoding.UTF8.GetString(stream.ToArray());

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var matches = JsonSerializer.Deserialize<List<MatchData>>(jsonString, options);

                if (matches == null)
                {
                    // Pokušaj deserializacije kao single objekat
                    var singleMatch = JsonSerializer.Deserialize<MatchData>(jsonString, options);
                    if (singleMatch != null)
                    {
                        matches = new List<MatchData> { singleMatch };
                    }
                    else
                    {
                        TempData["Error"] = "Neispravan JSON format.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                var viewModel = new EmailGeneratorViewModel
                {
                    Matches = matches
                };

                TempData["Success"] = $"Uspešno učitano {matches.Count} recruiters.";
                return View("Index", viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Greška pri učitavanju fajla: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendEmail([FromBody] SendEmailRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.To) ||
                    string.IsNullOrEmpty(request.Subject) || string.IsNullOrEmpty(request.Body))
                {
                    return BadRequest(new { success = false, error = "Missing required fields" });
                }

                // Putanja do CV fajla (pretpostavićemo da je u wwwroot/cv/Marko_Keser_CV.pdf)
                var cvPath = Path.Combine(_env.WebRootPath, "cv", "Marko Keser CV.pdf");

                // Ako fajl ne postoji, probaj alternativnu putanju
                if (!System.IO.File.Exists(cvPath))
                {
                    cvPath = Path.Combine(_env.ContentRootPath, "wwwroot", "cv", "Marko Keser CV.pdf");
                }

                // Loguj za debug
                _logger.LogInformation($"Looking for CV at: {cvPath}");
                _logger.LogInformation($"File exists: {System.IO.File.Exists(cvPath)}");

                // Pošalji mejl
                var sent = await _emailService.SendEmailWithAttachmentAsync(
                    request.To,
                    request.Subject,
                    request.Body,
                    cvPath
                );

                if (sent)
                {
                    // Sačuvaj u bazu ili log (opciono)
                    _logger.LogInformation($"Email sent to {request.To} for {request.RecruiterName}");

                    return Ok(new
                    {
                        success = true,
                        message = "Email successfully sent with CV attachment"
                    });
                }
                else
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        error = "Failed to send email"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in SendEmail: {ex.Message}");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET: /EmailGeneration/GetSampleJson
        [HttpGet]
        public IActionResult GetSampleJson()
        {
            var sample = new List<MatchData>
            {
                new MatchData
                {
                    CompanyName = "Primer Kompanija",
                    Website = "https://primer.com",
                    City = "Barcelona",
                    Country = "Spain",
                    RecruiterName = "Ana Primercic",
                    RecruiterTitle = "IT Recruiter",
                    RecruiterEmail = "ana@primer.com",
                    EmailStatus = "Verified",
                    MatchScore = 8,
                    Reasoning = "Primer razloga zašto je dobar match...",
                    CompanyAnalysis = "Kompanija se bavi IT konsaltingom...",
                    LocationMatch = "Barcelona - idealno",
                    IndustryMatch = "IT industrija - dobar match",
                    KeyFindings = "Dobar potencijal",
                    Strengths = new List<string> { "Iskustvo sa .NET Core", "SQL optimizacija" },
                    Weaknesses = new List<string> { "Manje iskustva sa cloudom" }
                }
            };

            return Json(sample);
        }

        [HttpPost]
        public async Task<IActionResult> GenerateEmail([FromBody] GenerateEmailRequest request)
        {
            try
            {
                if (request?.MatchData == null)
                    return BadRequest(new { error = "Match data is required" });

                // Koristi CV iz request-a umesto LoadCVData()
                var cvData = request.CvData;

                // Konvertuj MatchData u AIMatchResponse
                var matchResponse = MapToAIMatchResponse(request.MatchData);

                // Kreiraj Recruiter objekat iz MatchData
                var recruiter = new Recruiter
                {
                    FirstName = request.MatchData.RecruiterName?.Split(' ').FirstOrDefault() ?? "Recruiter",
                    LastName = request.MatchData.RecruiterName?.Split(' ').Skip(1).FirstOrDefault() ?? "",
                    Title = request.MatchData.RecruiterTitle,
                    CompanyNameForEmails = request.MatchData.CompanyName,
                    Website = request.MatchData.Website,
                    City = request.MatchData.City,
                    Country = request.MatchData.Country,
                    CompanyCity = request.MatchData.City,
                    EmailStatus = request.MatchData.EmailStatus,
                    Email = request.MatchData.RecruiterEmail
                };

                // Generiši email preko servisa
                var emailContent = await _emailService.GenerateEmail(recruiter, cvData, matchResponse);

                return Ok(new
                {
                    success = true,
                    subject = emailContent.Subject,
                    body = emailContent.Body,
                    tone = emailContent.Tone,
                    keySellingPoints = emailContent.KeySellingPoints
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Opciono - za batch generisanje
        [HttpPost]
        public async Task<IActionResult> GenerateBatch([FromBody] List<MatchData> matches)
        {
            try
            {
                if (matches == null || !matches.Any())
                    return BadRequest(new { error = "No matches provided" });

                var cvData = await LoadCVData();
                var results = new List<object>();

                foreach (var match in matches)
                {
                    try
                    {
                        var matchResponse = MapToAIMatchResponse(match);
                        var recruiter = new Recruiter
                        {
                            FirstName = match.RecruiterName?.Split(' ').FirstOrDefault() ?? "Recruiter",
                            LastName = match.RecruiterName?.Split(' ').Skip(1).FirstOrDefault() ?? "",
                            Title = match.RecruiterTitle,
                            CompanyNameForEmails = match.CompanyName,
                            Website = match.Website,
                            City = match.City,
                            Country = match.Country,
                            CompanyCity = match.City,
                            EmailStatus = match.EmailStatus,
                            Email = match.RecruiterEmail
                        };

                        var emailContent = await _emailService.GenerateEmail(recruiter, cvData, matchResponse);

                        results.Add(new
                        {
                            companyName = match.CompanyName,
                            success = true,
                            subject = emailContent.Subject,
                            body = emailContent.Body,
                            tone = emailContent.Tone
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new
                        {
                            companyName = match.CompanyName,
                            success = false,
                            error = ex.Message
                        });
                    }
                }

                return Ok(new { success = true, results });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Pomoćne metode
        private AIMatchResponse MapToAIMatchResponse(MatchData match)
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

        private async Task<string> LoadCVData()
        {
            // Učitaj CV iz fajla ili baze
            return await Task.FromResult(@"
                Marko Keser - Backend Developer
                Location: Belgrade, Serbia (targeting Barcelona, Spain)
                
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
                Certifications: English C1, C# Advanced, Salesforce Developer I
            ");
        }
    }

    // Request model za generisanje emaila
    public class GenerateEmailRequest
    {
        public MatchData MatchData { get; set; }
        public string CvData { get; set; }  // ← DODATO
        public bool IncludeSignature { get; set; } = true;
    }
}
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Mvc;
using Recruiter_Scanner.Models;
using Recruiter_Scanner.Services;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Recruiter_Scanner.Controllers
{
    public class HomeController : Controller
    {
        private const int MaxRecruitersPerUpload = 10;

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly ILogger<HomeController> _logger;
        private readonly IAIService _aiService;
        private readonly IWebHostEnvironment _env;

        public HomeController(ILogger<HomeController> logger, IAIService aiService, IWebHostEnvironment env)
        {
            _logger = logger;
            _aiService = aiService;
            _env = env;
        }

        // GET: /
        public IActionResult Index()
        {
            return View(new RecruiterUploadViewModel());
        }

        // GET: /Home/MatchForm
        public IActionResult MatchForm()
        {
            return View();
        }

        // POST: /Home/Upload
        [HttpPost]
        public IActionResult Upload(IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
                return UploadError("Please choose a CSV or JSON file.");

            var extension = Path.GetExtension(csvFile.FileName).ToLowerInvariant();
            if (extension != ".csv" && extension != ".json")
                return UploadError("Only CSV and JSON files are supported.");

            List<Recruiter> recruiters;
            try
            {
                using var stream = csvFile.OpenReadStream();
                recruiters = extension == ".csv" ? ParseCsv(stream) : ParseJson(stream);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read uploaded file {FileName}", csvFile.FileName);
                return UploadError($"Could not read the file: {ex.Message}");
            }

            if (recruiters.Count == 0)
                return UploadError("No valid recruiters were found in the file.");

            if (recruiters.Count > MaxRecruitersPerUpload)
                return UploadError($"The file contains {recruiters.Count} recruiters — the limit is {MaxRecruitersPerUpload} per run.");

            return View("Index", new RecruiterUploadViewModel
            {
                Recruiters = recruiters,
                ShowResults = true
            });
        }

        // GET: /Home/UseDemoFile
        public IActionResult UseDemoFile()
        {
            var demoFilePath = Path.Combine(_env.WebRootPath, "demo", "demo_recruiters.csv");

            if (!System.IO.File.Exists(demoFilePath))
            {
                TempData["ErrorMessage"] = "Demo file not found.";
                return RedirectToAction(nameof(Index));
            }

            var bytes = System.IO.File.ReadAllBytes(demoFilePath);
            var demoFile = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "csvFile", "demo_recruiters.csv")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/csv"
            };

            return Upload(demoFile);
        }

        // POST: /Home/AnalyzeMatch
        [HttpPost]
        public async Task<IActionResult> AnalyzeMatch([FromBody] AIMatchRequest request)
        {
            if (request?.Recruiter == null || string.IsNullOrWhiteSpace(request.CVData))
                return Json(new { success = false, message = "Invalid request data" });

            try
            {
                var result = await _aiService.AnalyzeMatch(request.Recruiter, request.CVData);
                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI match analysis failed for {Company}", request.Recruiter.CompanyNameForEmails);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Home/BatchAnalyze
        [HttpPost]
        public async Task<IActionResult> BatchAnalyze([FromBody] BatchAnalyzeRequest request)
        {
            if (request?.Recruiters == null || string.IsNullOrWhiteSpace(request.CVData))
                return Json(new { success = false, message = "Invalid request data" });

            try
            {
                var results = new List<AIMatchResponse>();
                foreach (var recruiter in request.Recruiters.Take(MaxRecruitersPerUpload))
                {
                    results.Add(await _aiService.AnalyzeMatch(recruiter, request.CVData));
                }

                return Json(new { success = true, data = results });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch analysis failed");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Home/ExtractCVFromPDF
        [HttpPost]
        public async Task<IActionResult> ExtractCVFromPDF(IFormFile pdfFile)
        {
            if (pdfFile == null || pdfFile.Length == 0)
                return Json(new { success = false, message = "No file uploaded" });

            if (Path.GetExtension(pdfFile.FileName).ToLowerInvariant() != ".pdf")
                return Json(new { success = false, message = "Please upload a PDF file" });

            try
            {
                using var stream = pdfFile.OpenReadStream();
                var formattedCv = await _aiService.ExtractCVFromPDF(stream, pdfFile.FileName);
                return Json(new { success = true, data = formattedCv });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CV extraction failed for {FileName}", pdfFile.FileName);
                return Json(new { success = false, message = ex.Message });
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private IActionResult UploadError(string message)
        {
            return View("Index", new RecruiterUploadViewModel { ErrorMessage = message });
        }

        /// <summary>
        /// Parses an Apollo-style CSV export (see wwwroot/demo/demo_recruiters.csv).
        /// </summary>
        private static List<Recruiter> ParseCsv(Stream stream)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                TrimOptions = TrimOptions.Trim,
                PrepareHeaderForMatch = args => args.Header.Trim(),
                MissingFieldFound = null,
                HeaderValidated = null,
                BadDataFound = null
            };

            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, config);
            csv.Context.RegisterClassMap<RecruiterCsvMap>();

            return csv.GetRecords<Recruiter>()
                .Where(r => !string.IsNullOrWhiteSpace(r.CompanyNameForEmails) || !string.IsNullOrWhiteSpace(r.Email))
                .ToList();
        }

        /// <summary>
        /// Parses a JSON array of recruiters (same shape as the outreach results export).
        /// </summary>
        private static List<Recruiter> ParseJson(Stream stream)
        {
            var items = JsonSerializer.Deserialize<List<JsonRecruiter>>(stream, JsonOptions) ?? new List<JsonRecruiter>();

            return items.Select(item =>
            {
                var nameParts = (item.recruiterName ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                return new Recruiter
                {
                    CompanyNameForEmails = item.companyName ?? string.Empty,
                    Website = item.website ?? string.Empty,
                    City = item.city ?? string.Empty,
                    Country = item.country ?? string.Empty,
                    FirstName = nameParts.ElementAtOrDefault(0) ?? string.Empty,
                    LastName = nameParts.ElementAtOrDefault(1) ?? string.Empty,
                    Title = item.recruiterTitle ?? string.Empty,
                    Email = item.recruiterEmail ?? string.Empty,
                    EmailStatus = item.emailStatus ?? string.Empty,
                    PersonLinkedinUrl = item.linkedinProfile ?? string.Empty,
                    CompanyLinkedinUrl = item.linkedinCompany ?? string.Empty
                };
            }).ToList();
        }

        public class BatchAnalyzeRequest
        {
            public List<Recruiter> Recruiters { get; set; } = new();
            public string CVData { get; set; } = string.Empty;
        }
    }
}

using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Mvc;
using Recruiter_Scanner.Models;
using Recruiter_Scanner.Services;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using CsvHelper.Configuration;

namespace Recruiter_Scanner.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger, IAIService service)
        {
            _logger = logger;
            _aiService = service;
        }

        private readonly IAIService _aiService;


        [HttpPost]
        public IActionResult Upload(IFormFile csvFile)
        {
            var model = new RecruiterUploadViewModel();

            if (csvFile == null || csvFile.Length == 0)
            {
                model.ErrorMessage = "Molimo odaberite fajl (CSV ili JSON).";
                model.Recruiters = new List<Recruiter>();
                return View("Index", model);
            }

            var fileExtension = Path.GetExtension(csvFile.FileName).ToLowerInvariant();

            // Provera da li je CSV ili JSON
            if (fileExtension != ".csv" && fileExtension != ".json")
            {
                model.ErrorMessage = "Molimo odaberite CSV ili JSON fajl.";
                model.Recruiters = new List<Recruiter>();
                return View("Index", model);
            }

            try
            {
                var recruiters = new List<Recruiter>();

                // ===== CSV OBRADA (potpuno tvoj originalni kod) =====
                if (fileExtension == ".csv")
                {
                    using var reader = new StreamReader(csvFile.OpenReadStream(), Encoding.UTF8);
                    string line;
                    bool isFirstLine = true;
                    string[] headers = null;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (isFirstLine)
                        {
                            headers = ParseCsvLine(line);
                            isFirstLine = false;
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        try
                        {
                            var values = ParseCsvLine(line);
                            if (values.Length > 0)
                            {
                                var recruiter = MapToRecruiter(headers, values);
                                recruiters.Add(recruiter);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Greška u redu: {ex.Message}");
                        }
                    }
                }
                // ===== JSON OBRADA =====
                else // .json
                {
                    using var reader = new StreamReader(csvFile.OpenReadStream(), Encoding.UTF8);
                    var jsonContent = reader.ReadToEnd();

                    // Parsiranje JSON niza - DIREKTNO U LISTU
                    var jsonRecruiters = System.Text.Json.JsonSerializer.Deserialize<List<JsonRecruiter>>(jsonContent, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (jsonRecruiters != null)
                    {
                        foreach (var jsonRec in jsonRecruiters)
                        {
                            try
                            {
                                // Parsiramo puno ime u FirstName i LastName
                                var fullName = jsonRec.recruiterName ?? "";
                                var firstName = "";
                                var lastName = "";

                                if (!string.IsNullOrWhiteSpace(fullName))
                                {
                                    var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length > 0)
                                    {
                                        firstName = parts[0];
                                        if (parts.Length > 1)
                                        {
                                            lastName = string.Join(" ", parts.Skip(1));
                                        }
                                    }
                                }

                                var recruiter = new Recruiter
                                {
                                    CompanyNameForEmails = jsonRec.companyName ?? "",
                                    Website = jsonRec.website ?? "",
                                    City = jsonRec.city ?? "",
                                    Country = jsonRec.country ?? "",
                                    FirstName = firstName,
                                    LastName = lastName,
                                    Title = jsonRec.recruiterTitle ?? "",
                                    Email = jsonRec.recruiterEmail ?? "",
                                    EmailStatus = jsonRec.emailStatus ?? "",
                                    CorporatePhone = "",
                                    PersonLinkedinUrl = ""
                                };

                                recruiters.Add(recruiter);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Greška pri obradi JSON objekta: {ex.Message}");
                            }
                        }
                    }
                }
                if (recruiters.Count > 10)
                {
                    ModelState.AddModelError("", "Not possible to add more than 10 recruiters");
                    // Vratite isti view sa greškom
                    model = new RecruiterUploadViewModel
                    {
                        ErrorMessage = "Not possible to add more than 10 recruiters",
                        ShowResults = false,
                        Recruiters = new List<Recruiter>()
                    };
                    return View("Index", model); // Ili naziv vašeg view-a
                }

                model.Recruiters = recruiters;
                model.ShowResults = true;

                if (recruiters.Count == 0)
                {
                    model.ErrorMessage = $"Nema validnih podataka u fajlu.";
                    model.Recruiters = new List<Recruiter>();
                }
            }
            catch (Exception ex)
            {
                model.ErrorMessage = $"Greška pri čitanju fajla: {ex.Message}";
                model.Recruiters = new List<Recruiter>();
            }

            return View("Index", model);
        }

        public IActionResult UseDemoFile()
        {
            var demoFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "demo", "demo_recruiters.csv");

            if (!System.IO.File.Exists(demoFilePath))
            {
                TempData["ErrorMessage"] = "Demo file not found.";
                return RedirectToAction("Index");
            }

            // Pročitaj fajl i kreiraj IFormFile
            var fileBytes = System.IO.File.ReadAllBytes(demoFilePath);
            var stream = new MemoryStream(fileBytes);
            var formFile = new FormFile(stream, 0, fileBytes.Length, "csvFile", "demo_recruiters.csv")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/csv"
            };

            // Direktno pozovi Upload metodu sa fajlom
            return Upload(formFile);
        }


        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Dvostruki navodnici unutar polja - dodaj jedan
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        // Prekidač za navodnike
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    // Kraj polja
                    result.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            // Dodaj poslednje polje
            result.Add(currentField.ToString());

            return result.ToArray();
        }

        private Recruiter MapToRecruiter(string[] headers, string[] values)
        {
            var recruiter = new Recruiter();

            for (int i = 0; i < headers.Length && i < values.Length; i++)
            {
                var header = headers[i].Trim().Trim('"');
                var value = values[i].Trim().Trim('"');

                switch (header)
                {
                    case "Company Name for Emails":
                        recruiter.CompanyNameForEmails = value;
                        break;
                    case "Website":
                        recruiter.Website = value;
                        break;
                    case "First Name":
                        recruiter.FirstName = value;
                        break;
                    case "Last Name":
                        recruiter.LastName = value;
                        break;
                    case "Person Linkedin Url":
                        recruiter.PersonLinkedinUrl = value;
                        break;
                    case "Title":
                        recruiter.Title = value;
                        break;
                    case "Email":
                        recruiter.Email = value;
                        break;
                    case "Email Status":
                        recruiter.EmailStatus = value;
                        break;
                    case "Corporate Phone":
                        recruiter.CorporatePhone = value;
                        break;
                    case "# Employees":
                        recruiter.NumberOfEmployees = value;
                        break;
                    case "Company Linkedin Url":
                        recruiter.CompanyLinkedinUrl = value;
                        break;
                    case "Facebook Url":
                        recruiter.FacebookUrl = value;
                        break;
                    case "Twitter Url":
                        recruiter.TwitterUrl = value;
                        break;
                    case "City":
                        recruiter.City = value;
                        break;
                    case "State":
                        recruiter.State = value;
                        break;
                    case "Country":
                        recruiter.Country = value;
                        break;
                    case "Company Address":
                        recruiter.CompanyAddress = value;
                        break;
                    case "Company City":
                        recruiter.CompanyCity = value;
                        break;
                }
            }

            return recruiter;
        }

        // POST: Recruiter/AnalyzeMatch
        [HttpPost]
        public async Task<IActionResult> AnalyzeMatch([FromBody] AIMatchRequest request)
        {
            try
            {
                if (request.Recruiter == null || string.IsNullOrEmpty(request.CVData))
                {
                    return Json(new { success = false, message = "Invalid request data" });
                }

                var result = await _aiService.AnalyzeMatch(request.Recruiter, request.CVData);

                return Json(new
                {
                    success = true,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // GET: Recruiter/MatchForm
        public IActionResult Index()
        {
            var model = new RecruiterUploadViewModel
            {
                Recruiters = new List<Recruiter>(),
                ShowResults = false
            };
            return View(model);
        }

        public IActionResult MatchForm()
        {
            
            return View();
        }


        // POST: Recruiter/BatchAnalyze
        [HttpPost]
        public async Task<IActionResult> BatchAnalyze([FromBody] BatchAnalyzeRequest request)
        {
            try
            {
                var results = new List<AIMatchResponse>();

                foreach (var recruiter in request.Recruiters.Take(10)) // Limit na 10 za batch
                {
                    var result = await _aiService.AnalyzeMatch(recruiter, request.CVData);
                    results.Add(result);

                    // Mala pauza da ne preoptereti API
                    break;
                 //   await Task.Delay(500);
                }

                return Json(new { success = true, data = results });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: Home/ExtractCVFromPDF
        [HttpPost]
        public async Task<IActionResult> ExtractCVFromPDF(IFormFile pdfFile)
        {
            try
            {
                if (pdfFile == null || pdfFile.Length == 0)
                {
                    return Json(new { success = false, message = "No file uploaded" });
                }

                if (Path.GetExtension(pdfFile.FileName).ToLowerInvariant() != ".pdf")
                {
                    return Json(new { success = false, message = "Please upload a PDF file" });
                }

                using (var stream = pdfFile.OpenReadStream())
                {
                    string formattedCV = await _aiService.ExtractCVFromPDF(stream, pdfFile.FileName);

                    return Json(new
                    {
                        success = true,
                        data = formattedCV
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        public class BatchAnalyzeRequest
        {
            public List<Recruiter> Recruiters { get; set; }
            public string CVData { get; set; }
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
    }
}

    using Recruiter_Scanner.Models;
    using System.Text.Json;
    using System.Text;
    using Microsoft.Extensions.Options;
    using global::Recruiter_Scanner.Models;
using Microsoft.Extensions.Hosting;
using static System.Collections.Specialized.BitVector32;
using static System.Formats.Asn1.AsnWriter;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Drawing;
using System.Numerics;
using System.Security.Cryptography.Xml;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml;
using System;
using System.Net.Mail;
using System.Net;

    namespace Recruiter_Scanner.Services
    {
    public interface IEmailGenerationService
    {
        Task<EmailContent> GenerateEmail(Recruiter recruiter, string cvData, AIMatchResponse matchAnalysis);
        Task<EmailContent> GenerateLinkedInMessage(Recruiter recruiter, string cvData, AIMatchResponse matchAnalysis); // NOVO
        Task<bool> SendEmailWithAttachmentAsync(string to, string subject, string body, string attachmentPath);
    }

    public class OpenAIEmailService : IEmailGenerationService
        {
            private readonly HttpClient _httpClient;
            private readonly IConfiguration _configuration;
            private readonly string _apiKey;
            private readonly string _apiUrl = "https://api.openai.com/v1/chat/completions";
            private readonly string _model;
            private readonly ILogger<OpenAIEmailService> _logger;

        public OpenAIEmailService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAIEmailService> logger)
            {
                _httpClient = httpClient;
                _configuration = configuration;
                _apiKey = Environment.GetEnvironmentVariable("AI_PASS");
            _model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";
                _logger = logger;
            }

        public async Task<EmailContent> GenerateLinkedInMessage(Recruiter recruiter, string cvData, AIMatchResponse matchAnalysis)
        {
            try
            {
                var prompt = BuildLinkedInPrompt(recruiter, cvData, matchAnalysis);

                var requestBody = new
                {
                    model = _model,
                    messages = new[]
                    {
                new {
                    role = "system",
                    content = @"You are an expert at writing professional LinkedIn messages to recruiters.
                    You help .NET developers craft personalized, effective connection requests and follow-up messages.
                    You know how to be concise, respectful, and engaging on LinkedIn's platform.
                    Messages should be friendly, professional, and optimized for LinkedIn's character limits.
                    You write in a natural tone that gets responses without being pushy."
                },
                new { role = "user", content = prompt }
            },
                    temperature = 0.7,
                    max_tokens = 500, // LinkedIn poruke treba da budu kraće
                    response_format = new { type = "json_object" }
                };

                var requestJson = JsonSerializer.Serialize(requestBody);
                var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                var response = await _httpClient.PostAsync(_apiUrl, requestContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"API Error: {response.StatusCode} - {errorContent}");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("choices", out var choices) &&
                    choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var content))
                    {
                        var aiContent = content.GetString();

                        // Očisti JSON
                        var lines = aiContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        string cleanedJson = string.Join("\n", lines);

                        cleanedJson = System.Text.RegularExpressions.Regex.Replace(cleanedJson, @",\s*,", ",");
                        cleanedJson = System.Text.RegularExpressions.Regex.Replace(cleanedJson, @"\{\s*,", "{");
                        cleanedJson = System.Text.RegularExpressions.Regex.Replace(cleanedJson, @",\s*\}", "}");

                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };

                        var emailContent = JsonSerializer.Deserialize<EmailContent>(cleanedJson, options);

                        if (emailContent != null)
                        {
                            return emailContent;
                        }
                    }
                }

                // Fallback u slučaju greške
                return new EmailContent
                {
                    Subject = $"Connecting regarding .NET opportunities",
                    Body = GenerateFallbackLinkedInMessage(recruiter),
                    Tone = "Professional",
                    KeySellingPoints = new List<string> { ".NET experience", "Backend development", "Barcelona-based" },
                    Status = "Generated with fallback template"
                };
            }
            catch (Exception ex)
            {
                return new EmailContent
                {
                    Subject = $"Connecting regarding .NET opportunities",
                    Body = GenerateFallbackLinkedInMessage(recruiter),
                    Tone = "Professional",
                    KeySellingPoints = new List<string> { ".NET experience", "Backend development" },
                    Status = $"Error: {ex.Message}"
                };
            }
        }

        private string GenerateFallbackLinkedInMessage(Recruiter recruiter)
        {
            return $@"Hi {recruiter.FirstName}, I'm a .NET backend developer based in Barcelona. I follow {recruiter.CompanyNameForEmails}'s work in tech and would love to connect and learn more about potential opportunities with your team. Thanks!";
        }

        private string BuildLinkedInPrompt(Recruiter recruiter, string cvData, AIMatchResponse matchAnalysis)
        {
            return $@"Write a LinkedIn message to a recruiter following these EXACT examples:

RECRUITER INFO:
- Name: {recruiter.FirstName} {recruiter.LastName}
- Company: {recruiter.CompanyNameForEmails}
- Title: {recruiter.Title ?? "Recruiter"}
- Company focus: {matchAnalysis.IndustryMatch ?? "IT recruitment"}
- What they do: {matchAnalysis.CompanyAnalysis ?? "technology recruitment"}

CANDIDATE (MARKO KESER):
- Role: Backend Developer
- Core stack: C#/.NET
- Experience: ~2 years
- Location: Barcelona (already based in Barcelona)
- Skills: APIs, automation, AI integration work, performance optimization
- GitHub: github.com/markokeser
- Live project: zubac-matine-production.up.railway.app

REFERENCE EXAMPLES (use exactly this style):

EXAMPLE 1 - Connection Request with introduction:
Hi {recruiter.FirstName},

I'm Marko, a C#/.NET backend developer based in Barcelona with around two years of experience building APIs and automation tools. I've been following {recruiter.CompanyNameForEmails} and noticed you specialize in placing tech talent in Barcelona's startup scene. Would be great to connect and learn about any .NET opportunities you're currently working on.

EXAMPLE 2 - Connection Request with company research:
Hi {recruiter.FirstName},

I'm Marko, a .NET developer based in Barcelona. I came across {recruiter.CompanyNameForEmails} while researching tech recruitment in the area and see you work with several local tech companies. I'm wondering if you're currently looking for backend developers with C# experience - would love to connect and hear more.

EXAMPLE 3 - Follow-up with introduction:
Hi {recruiter.FirstName},

Thanks for connecting! I'm Marko, a backend developer based in Barcelona with experience in C#/.NET, APIs, and performance optimization. Given that {recruiter.CompanyNameForEmails} focuses on connecting developers with Barcelona-based tech companies, I was hoping you might have some insight into local .NET opportunities. I'd love to chat if you're working on any relevant roles.

EXAMPLE 4 - Follow-up with specific interest:
Hi {recruiter.FirstName},

Great to connect! I'm Marko, a .NET developer based in Barcelona. I've been exploring the local tech scene and noticed that {recruiter.CompanyNameForEmails} has a strong presence in placing .NET developers. With my background in building scalable APIs and automation tools, I'm curious if you're currently recruiting for any backend positions. Happy to jump on a quick call to discuss further.

YOUR TASK:
Write a LinkedIn message that follows this EXACT style and length.

RULES:
1. Length: 3-5 sentences (like the examples above)
2. ALWAYS start with ""Hi [Name],"", then new line, then ""I'm Marko...""
3. Always state you're BASED IN BARCELONA
4. Company research: Add ONE natural sentence showing you've looked into them
   - Use phrases like: ""I noticed {recruiter.CompanyNameForEmails} focuses on..."", ""I've been following {recruiter.CompanyNameForEmails}'s work in..."", ""I see that {recruiter.CompanyNameForEmails} works with...""
5. Mention what you like/noticed about their company (specific, not generic)
6. Structure: Hi [Name] -> I'm Marko -> experience -> company research -> question/interest
7. Skills: Mention .NET, APIs, automation (keep it brief)
8. NO previous companies (no Wicked Games, Quadro, etc.)
9. NO generic compliments like ""I'm a big fan""
10. NO emojis
11. End with a soft question or invitation to connect/chat

Return a JSON with this structure:
{{
    ""subject"": ""Connection request"",
    ""body"": ""The LinkedIn message (plain text, with proper line breaks)"",
    ""tone"": ""Professional"",
    ""keySellingPoints"": [""NET experience"", ""Barcelona-based"", ""API development"", ""automation""],
    ""status"": ""Success""
}}";
        }

        public async Task<bool> SendEmailWithAttachmentAsync(string to, string subject, string body, string attachmentPath)
        {
            try
            {
                // Učitaj SMTP podešavanja iz appsettings.json
                var smtpServer = _configuration["Email:SmtpServer"];
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
                var smtpUsername = _configuration["Email:Username"];
                var smtpPassword = _configuration["Email:Password"];
                var fromEmail = _configuration["Email:FromEmail"];
                var fromName = _configuration["Email:FromName"] ?? "Marko Keser";

                using var message = new MailMessage();
                message.From = new MailAddress(fromEmail, fromName);
                message.To.Add(to);
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = false; // Set na true ako želiš HTML mejlove

                // Dodaj CV attachment
                if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
                {
                    var attachment = new Attachment(attachmentPath);
                    attachment.ContentDisposition.Inline = false;
                    attachment.ContentDisposition.DispositionType = "attachment";
                    message.Attachments.Add(attachment);
                }
                else
                {
                    _logger.LogWarning($"Attachment not found: {attachmentPath}");
                }

                using var client = new SmtpClient(smtpServer, smtpPort);
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                client.DeliveryMethod = SmtpDeliveryMethod.Network;

                await client.SendMailAsync(message);

                _logger.LogInformation($"Email sent successfully to {to}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending email: {ex.Message}");
                return false;
            }
        }

        public async Task<EmailContent> GenerateEmail(Recruiter recruiter, string cvData, AIMatchResponse matchAnalysis)
            {
                try
                {
                    var prompt = BuildEmailPrompt(recruiter, cvData, matchAnalysis);

                    var requestBody = new
                    {
                        model = _model,
                        messages = new[]
                        {
                        new {
                            role = "system",
                            content = @"You are an expert at writing professional outreach emails to recruiters.
                            You help .NET developers craft personalized, effective emails that get responses.
                            You know how to highlight relevant experience, show genuine interest in the company,
                            and make a strong impression without being too pushy or generic.
                            You write in a natural, professional tone that sounds like a real person, not a template."
                        },
                        new { role = "user", content = prompt }
                    },
                        temperature = 0.7,
                        max_tokens = 800,
                        response_format = new { type = "json_object" }
                    };

                    var requestJson = JsonSerializer.Serialize(requestBody);
                    var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                    var response = await _httpClient.PostAsync(_apiUrl, requestContent);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        throw new Exception($"API Error: {response.StatusCode} - {errorContent}");
                    }

                    var responseJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseJson);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("choices", out var choices) &&
                        choices.GetArrayLength() > 0)
                    {
                        var firstChoice = choices[0];
                        if (firstChoice.TryGetProperty("message", out var message) &&
                            message.TryGetProperty("content", out var content))
                        {
                            var aiContent = content.GetString();

                            // Očisti JSON (isti pattern kao u tvom servisu)
                            var lines = aiContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                            string cleanedJson = string.Join("\n", lines);

                            cleanedJson = System.Text.RegularExpressions.Regex.Replace(cleanedJson, @",\s*,", ",");
                            cleanedJson = System.Text.RegularExpressions.Regex.Replace(cleanedJson, @"\{\s*,", "{");
                            cleanedJson = System.Text.RegularExpressions.Regex.Replace(cleanedJson, @",\s*\}", "}");

                            var options = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            };

                            var emailContent = JsonSerializer.Deserialize<EmailContent>(cleanedJson, options);

                            if (emailContent != null)
                            {
                                return emailContent;
                            }
                        }
                    }

                    // Fallback u slučaju greške
                    return new EmailContent
                    {
                        Subject = $"Experienced .NET Developer Interested in Opportunities at {recruiter.CompanyNameForEmails}",
                        Body = GenerateFallbackEmail(recruiter),
                        Status = "Generated with fallback template"
                    };
                }
                catch (Exception ex)
                {
                    return new EmailContent
                    {
                        Subject = $"Regarding .NET Developer Position",
                        Body = $"Hello {recruiter.FirstName},\n\nI came across {recruiter.CompanyNameForEmails} and I'm very interested in potential .NET developer opportunities.\n\nI have 2+ years of experience with C#, ASP.NET Core, and have built production applications.\n\nWould you be open to a quick chat about how I could contribute to your team?\n\nBest regards,\nMarko Keser\nGitHub: github.com/markokeser",
                        Status = $"Error: {ex.Message}"
                    };
                }
            }

        /*    private string BuildEmailPrompt(Recruiter recruiter, string cvData, AIMatchResponse matchAnalysis)
            {
                return $@"Write a short outreach email to a recruiter following these EXACT examples:

    RECRUITER INFO:
    - Name: {recruiter.FirstName} {recruiter.LastName}
    - Company: {recruiter.CompanyNameForEmails}  (use this EXACT company name in the email)
    - Company focus: {matchAnalysis.IndustryMatch ?? "IT recruitment"}

    CANDIDATE (MARKO KESER):
    - Role: Backend Developer
    - Core stack: C#/.NET
    - Experience: ~2 years
    - Location: Barcelona
    - Skills: APIs, automation, AI integration work
    - Phone: +34 637 18 27 83
    - Website: kesermarko.com

    REFERENCE EXAMPLES (use exactly this style):

    EXAMPLE 1:
    Subject: Backend .NET developer – Barcelona

    Hi Elena,

    I'm a C#/.NET backend developer based in Barcelona with ~2 years of experience in APIs, automation, and some AI integration work.

    Are you currently working on any backend roles that could be a fit?

    CV attached. Happy to jump on a quick call if helpful.

    EXAMPLE 2:
    Subject: Backend .NET Developer – Barcelona

    Hi Elena,

    I'm a backend developer based in Barcelona with around two years of experience working primarily with C#/.NET. My work has focused on building APIs, improving backend performance, and contributing to process automation projects, including some AI-related integrations.

    I'm currently exploring new opportunities and would be interested in hearing if you're working on any backend roles that align with this background.

    I've attached my CV for context and would be happy to have a short call if useful.

    Best regards,
    Marko Keser
    +34 637 18 27 83
    kesermarko.com

    YOUR TASK:
    Write an email that follows this EXACT style and length.

    RULES:
    1. Length: Same as examples (short, 4-6 sentences)
    2. Structure: Hi [Name], introduction, question, CV mention, sign-off
    3. Company reference: Add ONE short, natural mention of what the company does, using the company name: {recruiter.CompanyNameForEmails}
       - Example: ""Noticing {recruiter.CompanyNameForEmails} works with [industry] companies""
       - Example: ""Given {recruiter.CompanyNameForEmails}'s focus on [sector]""
    4. Skills: Mention APIs, automation, AI integration (as shown in examples)
    5. NO previous companies (no Wicked Games, Quadro, etc.)
    6. NO generic compliments or flattery
    7. NO corporate buzzwords
    8. Signature: Name, phone, website

    Return a JSON with this structure:
    {{
        ""subject"": ""{recruiter.FirstName}, backend dev position?"",
        ""body"": ""Full email body with proper line breaks"",
        ""status"": ""Success""
    }}";
            }  */

        private string BuildEmailPrompt(Recruiter recruiter, string cvData, AIMatchResponse matchAnalysis)
        {
            return $@"Write a short outreach email to a recruiter based on this candidate's CV:

RECRUITER INFO:
- Name: {recruiter.FirstName} {recruiter.LastName}
- Company: {recruiter.CompanyNameForEmails}  (use this EXACT company name in the email)
- Company focus: {matchAnalysis.IndustryMatch ?? "IT recruitment"}

CANDIDATE CV:
{cvData}

REFERENCE EXAMPLES (use exactly this style):

EXAMPLE 1:
Subject: Backend .NET developer – Barcelona

Hi Elena,

I'm a C#/.NET backend developer based in Barcelona with ~2 years of experience in APIs, automation, and some AI integration work.

Are you currently working on any backend roles that could be a fit?

CV attached. Happy to jump on a quick call if helpful.

EXAMPLE 2:
Subject: Backend .NET Developer – Barcelona

Hi Elena,

I'm a backend developer based in Barcelona with around two years of experience working primarily with C#/.NET. My work has focused on building APIs, improving backend performance, and contributing to process automation projects, including some AI-related integrations.

I'm currently exploring new opportunities and would be interested in hearing if you're working on any backend roles that align with this background.

I've attached my CV for context and would be happy to have a short call if useful.

Best regards,
Marko Keser
+34 637 18 27 83
kesermarko.com

YOUR TASK:
Write an email that follows this EXACT style and length, using information from the candidate's CV above.

RULES:
1. Length: Same as examples (short, 4-6 sentences)
2. Structure: Hi [Name], introduction, question, CV mention, sign-off
3. Company reference: Add ONE short, natural mention of what the company does, using the company name: {recruiter.CompanyNameForEmails}
   - Example: ""Noticing {recruiter.CompanyNameForEmails} works with [industry] companies""
   - Example: ""Given {recruiter.CompanyNameForEmails}'s focus on [sector]""
4. Skills: Extract key skills from the CV and mention them naturally (focus on backend, APIs, automation, AI if present)
5. Experience: Mention years of experience if found in CV (otherwise keep general)
6. Location: Extract from CV if present (Barcelona/Spain or general)
7. NO previous company names from CV (don't mention specific past employers)
8. NO generic compliments or flattery
9. NO corporate buzzwords
10. Signature format:
    Best regards,
    [Name from CV]
    [Phone from CV - ONLY include if phone number is explicitly found in the CV, otherwise omit this line completely]
    [Website/Email from CV - ONLY include if website or email is explicitly found in the CV, otherwise omit this line completely]

    IMPORTANT: Only include phone and website lines if they actually appear in the CV. If they don't exist, just skip those lines entirely.

Return a JSON with this structure:
{{
    ""subject"": ""Short subject line (like the examples)"",
    ""body"": ""Full email body with proper line breaks"",
    ""status"": ""Success""
}}";
        }

        private string GenerateFallbackEmail(Recruiter recruiter)
            {
                return $@"Hello {recruiter.FirstName},

I came across {recruiter.CompanyNameForEmails} and I'm very interested in potential .NET developer opportunities with your team.

I'm a backend developer with ~2 years of experience in C#/.NET development. Currently working at Wicked Games as a Backend/Math Developer, and previously at Quadro Consulting. I have strong experience with ASP.NET Core, Entity Framework, SQL databases, and have built and deployed production applications.

Some highlights from my work:
- Built and deployed a production Restaurant Management System handling real-time data
- Active GitHub with projects showcasing my code
- Experience with both enterprise (consulting) and gaming industries

I'm currently based in Serbia but actively looking to relocate to Barcelona/Spain, and I'm open to remote opportunities as well.

Would you be open to a brief chat about how my experience might align with your current needs?

Best regards,
Marko Keser
GitHub: github.com/markokeser
Live project: zubac-matine-production.up.railway.app";
            }
        }

        public class EmailContent
        {
            public string Subject { get; set; }
            public string Body { get; set; }
            public string Tone { get; set; }
            public List<string> KeySellingPoints { get; set; } = new List<string>();
            public string Status { get; set; }
        }
    }

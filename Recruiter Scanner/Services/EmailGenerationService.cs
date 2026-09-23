using Recruiter_Scanner.Models;
using System.Net;
using System.Net.Mail;

namespace Recruiter_Scanner.Services
{
    public interface IEmailGenerationService
    {
        Task<EmailContent> GenerateEmail(Recruiter recruiter, string cvData, AIMatchResponse matchAnalysis);
        Task<EmailContent> GenerateLinkedInMessage(Recruiter recruiter, string cvData, AIMatchResponse matchAnalysis);
        Task<bool> SendEmailWithAttachmentAsync(string to, string subject, string body, string attachmentPath);
    }

    public class OpenAIEmailService : OpenAIServiceBase, IEmailGenerationService
    {
        private const string EmailSystemPrompt = @"You are an expert at writing professional outreach emails to recruiters.
You help .NET developers craft personalized, effective emails that get responses.
You know how to highlight relevant experience, show genuine interest in the company,
and make a strong impression without being too pushy or generic.
You write in a natural, professional tone that sounds like a real person, not a template.";

        private const string LinkedInSystemPrompt = @"You are an expert at writing professional LinkedIn messages to recruiters.
You help .NET developers craft personalized, effective connection requests and follow-up messages.
You know how to be concise, respectful, and engaging on LinkedIn's platform.
Messages should be friendly, professional, and optimized for LinkedIn's character limits.
You write in a natural tone that gets responses without being pushy.";

        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenAIEmailService> _logger;

        public OpenAIEmailService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAIEmailService> logger)
            : base(httpClient, configuration)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public Task<EmailContent> GenerateEmail(Recruiter recruiter, string cvData, AIMatchResponse matchAnalysis) =>
            CompleteJsonAsync<EmailContent>(EmailSystemPrompt, BuildEmailPrompt(recruiter, cvData, matchAnalysis), temperature: 0.7, maxTokens: 800);

        // LinkedIn messages are shorter, hence the lower token budget.
        public Task<EmailContent> GenerateLinkedInMessage(Recruiter recruiter, string cvData, AIMatchResponse matchAnalysis) =>
            CompleteJsonAsync<EmailContent>(LinkedInSystemPrompt, BuildLinkedInPrompt(recruiter, matchAnalysis), temperature: 0.7, maxTokens: 500);

        public async Task<bool> SendEmailWithAttachmentAsync(string to, string subject, string body, string attachmentPath)
        {
            var smtpServer = _configuration["Email:SmtpServer"];
            var fromEmail = _configuration["Email:FromEmail"];

            if (string.IsNullOrWhiteSpace(smtpServer) || string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogWarning("SMTP is not configured (Email:SmtpServer / Email:FromEmail); email to {To} was not sent", to);
                return false;
            }

            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, _configuration["Email:FromName"] ?? "Marko Keser"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };
                message.To.Add(to);

                if (File.Exists(attachmentPath))
                    message.Attachments.Add(new Attachment(attachmentPath));
                else
                    _logger.LogWarning("CV attachment not found at {Path}", attachmentPath);

                using var client = new SmtpClient(smtpServer, int.Parse(_configuration["Email:SmtpPort"] ?? "587"))
                {
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Credentials = new NetworkCredential(_configuration["Email:Username"], _configuration["Email:Password"])
                };

                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent to {To}", to);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {To}", to);
                return false;
            }
        }

        private static string BuildEmailPrompt(Recruiter recruiter, string cvData, AIMatchResponse matchAnalysis)
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

        private static string BuildLinkedInPrompt(Recruiter recruiter, AIMatchResponse matchAnalysis)
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

    }

    public class EmailContent
    {
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? Tone { get; set; }
        public List<string> KeySellingPoints { get; set; } = new();
        public string? Status { get; set; }
    }
}

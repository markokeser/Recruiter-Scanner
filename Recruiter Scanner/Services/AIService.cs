using Recruiter_Scanner.Models;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;

namespace Recruiter_Scanner.Services
{
    public interface IAIService
    {
        Task<AIMatchResponse> AnalyzeMatch(Recruiter recruiter, string cvData);
    }

    public class OpenAIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _apiKey;
        private readonly string _apiUrl = "https://api.openai.com/v1/chat/completions";
        private readonly string _model = "gpt-4o-mini";

        public OpenAIService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _apiKey = Environment.GetEnvironmentVariable("AI_PASS");
            _model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        }

        public async Task<AIMatchResponse> AnalyzeMatch(Recruiter recruiter, string cvData)
        {
            try
            {
                var prompt = BuildPrompt(recruiter, cvData);

                var requestBody = new
                {
                    model = _model,
                    messages = new[]
{
    new {
        role = "system",
        content = @"You are an expert technical recruiter with 15+ years of experience in IT recruitment, specializing in .NET and backend developer roles. 
You have perfect knowledge of the tech industry in Barcelona and Spain.
You are extremely analytical, honest, and provide actionable insights.
Your job is to help a .NET backend developer find the best matches from a list of companies and recruiters.
You analyze each opportunity thoroughly and give practical advice on how to approach it.
You never exaggerate or give false hope - if it's not a good match, you say so clearly and explain why."
    },
    new { role = "user", content = prompt }
},
                    temperature = 0.2,
                    max_tokens = 1000,
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
                        var aiContent = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

                        // ==== OČISTI JSON (ukloni prazne linije i viškove zareza) ====
                        // Ukloni prazne linije
                        var lines = aiContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        string cleanedJson = string.Join("\n", lines);

                        // Ukloni viškove zareza (npr. "score": 6,    ,"reasoning":...)
                        cleanedJson = System.Text.RegularExpressions.Regex.Replace(cleanedJson, @",\s*,", ",");
                        cleanedJson = System.Text.RegularExpressions.Regex.Replace(cleanedJson, @"\{\s*,", "{");
                        cleanedJson = System.Text.RegularExpressions.Regex.Replace(cleanedJson, @",\s*\}", "}");

                        // Sada parsiraj očišćeni JSON
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };

                        var aiResult = JsonSerializer.Deserialize<AIMatchResponse>(cleanedJson, options);

                        if (aiResult != null)
                        {
                            aiResult.Recruiter = recruiter;
                            return aiResult;
                        }

                        throw new Exception("Failed to parse AI response");
                    }
                }
            }
            catch (Exception ex)
            {
                return new AIMatchResponse
                {
                    Score = 0,
                    Reasoning = $"Error: {ex.Message}",
                    Recruiter = recruiter,
                    Strengths = new List<string>(),
                    Weaknesses = new List<string>()
                };
            }

            return new AIMatchResponse
            {
                Score = 0,
                Reasoning = $"Error: ",
                Recruiter = recruiter,
                Strengths = new List<string>(),
                Weaknesses = new List<string>()
            };
        }

        private string CleanJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return json;

            // 1. Ukloni prazne linije
            var lines = json.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            json = string.Join("\n", lines);

            // 2. Ukloni viškove zareza (npr. "score": 6,    ,"reasoning":...)
            // Ovo je komplikovanije, ali ćemo probati jednostavnije:

            // Zameni ",   ," sa ","
            json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*,", ",");

            // Zameni "{\s*," sa "{"
            json = System.Text.RegularExpressions.Regex.Replace(json, @"\{\s*,", "{");

            // Zameni ",\s*}" sa "}"
            json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*\}", "}");

            return json;
        }

        private AIMatchResponse ParseAIResponse(string responseJson, Recruiter recruiter)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                // OpenAI response format
                if (root.TryGetProperty("choices", out var choices) &&
                    choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var content))
                    {
                        var aiContent = content.GetString();

                        // Pokušaj parsirati JSON iz AI odgovora
                        try
                        {
                            var aiResponse = JsonSerializer.Deserialize<AIMatchResponse>(aiContent);
                            if (aiResponse != null)
                            {
                                aiResponse.Recruiter = recruiter;
                                return aiResponse;
                            }
                        }
                        catch
                        {
                            // Ako ne može da parsira JSON, vrati tekstualni odgovor
                            return new AIMatchResponse
                            {
                                Score = 5,
                                Reasoning = aiContent,
                                Recruiter = recruiter,
                                CompanyAnalysis = "See reasoning",
                                LocationMatch = "See reasoning",
                                IndustryMatch = "See reasoning",
                                KeyFindings = "See reasoning",
                                Strengths = new List<string>(),
                                Weaknesses = new List<string>()
                            };
                        }
                    }
                }

                throw new Exception("Unexpected API response format");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parsing AI response: {ex.Message}");
            }
        }

        /*    private string BuildPrompt(Recruiter recruiter, string cvData)
            {
                return $@"You are an expert technical recruiter and career advisor with deep knowledge of the IT industry, especially .NET ecosystem and backend development.

    CANDIDATE'S CV (MARKO KESER):
    {cvData}

    KEY FACTS ABOUT THE CANDIDATE:
    - Name: Marko Keser
    - Location: Belgrade, Serbia but actively looking in Barcelona, Spain
    - Core Expertise: Backend Developer with C# / .NET stack
    - Experience Level: ~2 years (Wicked Games + Quadro Consulting + internship)
    - Key Strengths: 
      * C#, ASP.NET Core, .NET 8+, Entity Framework
      * SQL Server, MySQL, Complex SQL queries, optimization
      * REST APIs, Authentication (RBAC, claims)
      * Some frontend (React, jQuery, HTML, CSS, JavaScript)
      * Cloud: Microsoft Azure basics, Railway deployment
      * AI integrations (OpenAI API)
    - Notable Achievement: Built and deployed production web app (Restaurant Management System) handling real-time data
    - Live project: zubac-matine-production.up.railway.app (can be verified)
    - Certifications: English C1, C# Advanced, Salesforce Developer I
    - GitHub: github.com/markokeser (active with projects)
    - Currently at: Wicked Games (Feb 2025 - Oct 2025) as Backend/Math Developer

    COMPANY/RECRUITER TO ANALYZE:
    - Company: {recruiter.CompanyNameForEmails}
    - Website: {recruiter.Website}
    - Recruiter: {recruiter.FirstName} {recruiter.LastName} - {recruiter.Title}
    - Location: {recruiter.City}, {recruiter.Country}
    - Company Address: {recruiter.CompanyAddress}
    - Company City: {recruiter.CompanyCity}
    - Email Status: {recruiter.EmailStatus} (how reliable is this contact)
    - Company LinkedIn: {recruiter.CompanyLinkedinUrl}
    - Facebook: {recruiter.FacebookUrl}
    - Twitter: {recruiter.TwitterUrl}
    - Employees: {recruiter.NumberOfEmployees}

    YOUR TASK:
    Analyze how well Marko Keser (the candidate) matches with this company/recruiter. Be extremely thorough and honest.

    CONSIDER THESE FACTORS:

    1. TECH STACK ALIGNMENT (most important):
       - Does the company need C#/.NET developers based on their name/website?
       - Would Marko's SQL skills be valuable?
       - Is there any indication of the technologies they use?
       - Look at company name, recruiter title, website for clues

    2. LOCATION FIT:
       - Marko is in Serbia but targeting Barcelona, Spain
       - Is this company in Barcelona or offering remote work?
       - Check if company location (City/Country) matches his target
       - Is the role likely remote-friendly?

    3. COMPANY TYPE & SIZE:
       - Number of employees: {recruiter.NumberOfEmployees} (indicates startup vs enterprise)
       - Based on company name, what industry are they in?
       - Would Marko's experience (enterprise at Quadro, gaming at Wicked) be relevant?

    4. RECRUITER RELEVANCE:
       - Recruiter title: {recruiter.Title} - does it indicate they hire tech roles?
       - Is this a specialized IT recruiter or general HR?
       - Email status: {recruiter.EmailStatus} - how likely is this to be a valid contact?

    5. CAREER PROGRESSION:
       - Marko has ~2 years experience
       - He's looking for backend developer roles
       - He has production experience and deployed apps
       - Is this role appropriate for his level?

    6. RED FLAGS / OPPORTUNITIES:
       - Any mismatch in tech stack
       - Location issues
       - Company size mismatch
       - Potential growth opportunities

    Based on ALL available information, provide a JSON response with EXACTLY this structure:

    {{
        ""score"": (integer 1-10, where 10 is perfect match),

        ""reasoning"": ""One paragraph summary of why this is or isn't a good match"",

        ""companyAnalysis"": ""What this company does, what tech they likely use, and whether it aligns with Marko's .NET backend expertise"",

        ""locationMatch"": ""Is this in Barcelona? Is it remote? How feasible is this for Marko given he's in Serbia but targeting Spain?"",

        ""industryMatch"": ""Is the industry (gaming, consulting, enterprise, startup, etc.) a good fit for Marko's background?"",

        ""keyFindings"": ""The single most important thing Marko should know about this opportunity"",

        ""strengths"": [
            ""Specific reason why Marko is a good fit (tied to his experience)"",
            ""Another specific strength with evidence from his CV"",
            ""Third specific strength""
        ],

        ""weaknesses"": [
            ""Specific gap or concern (be honest)"",
            ""Another potential weakness or area to address""
        ],

        ""contactStrategy"": ""How should Marko approach this recruiter? What to mention in email?"",

        ""recommendation"": ""Apply / Consider / Skip - brief explanation""
    }}

    BE SPECIFIC. Use evidence from Marko's CV. Don't make things up. If you're not sure about something, say so.

    Remember: You're helping a real developer find the right opportunities. Be honest, be helpful, be specific.";
            }  */

        private string BuildPrompt(Recruiter recruiter, string cvData)
        {
            return $@"You are an expert technical recruiter and career advisor with deep knowledge of the IT industry, especially .NET ecosystem and backend development.

CANDIDATE'S CV:
{cvData}

KEY FACTS ABOUT THE CANDIDATE (from their CV):
- Name: Extracted from CV
- Location: Extracted from CV (currently in Serbia/Eastern Europe)
- Core Expertise: Backend Developer with C# / .NET stack (2-3 years experience)
- Key Strengths: 
  * C#, ASP.NET Core, .NET 8+, Entity Framework
  * SQL Server, MySQL, Complex SQL queries, optimization
  * REST APIs, Authentication (RBAC, claims)
  * Cloud basics (Azure, Railway deployment)
  * English fluency (C1 level)
- Notable Achievement: Built and deployed production web app (live project)
- GitHub: Active with projects (can be verified)
- Work Authorization: Serbian citizen (non-EU), requires B2B/contractor setup

COMPANY/RECRUITER TO ANALYZE:
- Company: {recruiter.CompanyNameForEmails}
- Website: {recruiter.Website}
- Recruiter: {recruiter.FirstName} {recruiter.LastName} - {recruiter.Title}
- Location: {recruiter.City}, {recruiter.Country}
- Company Address: {recruiter.CompanyAddress}
- Company City: {recruiter.CompanyCity}
- Email Status: {recruiter.EmailStatus}
- Company LinkedIn: {recruiter.CompanyLinkedinUrl}
- Facebook: {recruiter.FacebookUrl}
- Twitter: {recruiter.TwitterUrl}
- Employees: {recruiter.NumberOfEmployees}

YOUR PRIMARY OBJECTIVE:
Analyze how well this candidate matches with this company specifically for a remote contractor (B2B) role. The candidate will work remotely from Serbia/Eastern Europe. The company may be anywhere in the world.

Your job is to assess the probability and strategy of converting this lead into a remote contract position.

CONSIDER THESE FACTORS FOR EACH COMPANY TYPE:

FOR TECHNOLOGY COMPANIES (GitHub, Microsoft, Google, etc.):
- These companies build developer tools and platforms
- They heavily use C#/.NET in their ecosystem
- They have distributed teams and hire remote contractors globally
- Candidate's GitHub activity and open source experience is HIGHLY valuable
- English fluency is essential
- Score should be 8-10/10 for tech companies

FOR FINANCIAL/TECH COMPANIES (Goldman Sachs, investment banks, fintech):
- These companies need backend developers for trading platforms, internal tools
- They use C#/.NET extensively in financial systems
- They have offices in expensive hubs (NY, London) and actively hire Eastern European contractors for cost savings
- Candidate's experience with complex SQL, authentication, and production apps is relevant
- No financial domain experience required - they train for domain
- Score should be 6-8/10 for financial companies

FOR RETAIL/NON-TECH COMPANIES (Zara/Inditex, traditional retail):
- These companies need developers for e-commerce, inventory, internal systems
- They often use C#/.NET in backend systems
- They may have limited remote contractor experience
- They prefer local candidates or agency contractors
- Candidate's e-commerce project experience is relevant but domain mismatch
- Score should be 3-5/10 for retail companies

SPECIFIC ANALYSIS FOR THE THREE DEMO COMPANIES:

1. GITHUB:
   - Perfect match for a .NET developer with GitHub activity
   - Company builds tools FOR developers
   - Candidate's GitHub profile and open source work is direct proof of value
   - Remote-first culture, hires globally
   - Score: 9/10

2. GOLDMAN SACHS:
   - Uses C#/.NET extensively in their tech stack
   - Has history of hiring Eastern European contractors
   - Candidate's SQL optimization and production experience valuable
   - No financial experience required
   - Score: 7/10

3. ZARA/INDITEX:
   - Uses C#/.NET but primarily for internal systems
   - Based in Spain, prefers local candidates
   - Limited remote contractor culture
   - Candidate's e-commerce project slightly relevant
   - Score: 4/10

Based on ALL available information, provide a JSON response with EXACTLY this structure:

{{""score"": (integer 1-10, use the guidelines above based on company type),
    
    ""reasoning"": ""One paragraph summary specifically about THIS company and why it is or isn't a good match."",
    
    ""companyAnalysis"": ""What this specific company does, what tech they use, and alignment with candidate's .NET expertise."",
    
    ""locationMatch"": ""Analysis of whether this company hires remote contractors from Eastern Europe/Serbia."",
    
    ""industryMatch"": ""How well the industry fits the candidate's background."",
    
    ""remoteContractorFit"": ""Specific likelihood this company would hire a B2B contractor from Serbia."",
    
    ""valueProposition"": ""What specific value this candidate offers THIS company (e.g., GitHub activity for GitHub, SQL skills for Goldman, etc.)."",
    
    ""keyFindings"": ""The most important thing to know about this opportunity."",
    
    ""strengths"": [
        ""Strength 1 specific to THIS company."",
        ""Strength 2 specific to THIS company."",
        ""Strength 3 specific to THIS company.""
    ],
    
    ""weaknesses"": [
        ""Weakness 1 specific to THIS company."",
        ""Weakness 2 specific to THIS company.""
    ],
    
    ""contactStrategy"": ""How to approach THIS specific recruiter - what to mention in email."",
    
    ""recommendation"": ""Pursue / Consider / Skip - with brief explanation.""
}}

IMPORTANT FORMATTING RULES:
- Use DOUBLE quotes for all JSON keys and string values.
- Use single quotes inside strings (like 'this').
- No special characters (č, ć, š, đ, ž).
- Be specific to THIS company, not generic.
- Use evidence from candidate's CV.
- Be honest but optimistic where appropriate.

Remember: You're helping a developer find remote opportunities globally.";
        }


    }
}

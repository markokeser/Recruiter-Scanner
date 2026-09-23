using Recruiter_Scanner.Models;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Recruiter_Scanner.Services
{
    public interface IAIService
    {
        Task<AIMatchResponse> AnalyzeMatch(Recruiter recruiter, string cvData);
        Task<string> ExtractCVFromPDF(Stream pdfStream, string? fileName = null);
    }

    public class OpenAIService : OpenAIServiceBase, IAIService
    {
        private const string CvParserSystemPrompt = @"You are an expert CV parser and technical recruiter. 
Your job is to extract information from raw CV text and format it into a clean, structured format.
You understand CV formats from different countries and can identify key sections like personal info, work experience, education, skills, etc.
You output ONLY the formatted CV text, no explanations or additional comments.";

        private const string MatchSystemPrompt = @"You are an expert technical recruiter with 15+ years of experience in IT recruitment, specializing in .NET and backend developer roles. 
You have perfect knowledge of the tech industry in Barcelona and Spain.
You are extremely analytical, honest, and provide actionable insights.
Your job is to help a .NET backend developer find the best matches from a list of companies and recruiters.
You analyze each opportunity thoroughly and give practical advice on how to approach it.
You never exaggerate or give false hope - if it's not a good match, you say so clearly and explain why.";

        public OpenAIService(HttpClient httpClient, IConfiguration configuration)
            : base(httpClient, configuration)
        {
        }

        /// <summary>
        /// Extracts text from a PDF and uses AI to format it into a structured CV.
        /// </summary>
        public async Task<string> ExtractCVFromPDF(Stream pdfStream, string? fileName = null)
        {
            var rawText = ExtractTextFromPdf(pdfStream);

            if (string.IsNullOrWhiteSpace(rawText))
                throw new InvalidOperationException("No text could be extracted from the PDF (is it a scanned image?).");

            // Low temperature for consistent formatting.
            return await CompleteAsync(CvParserSystemPrompt, BuildCVExtractionPrompt(rawText, fileName), temperature: 0.1, maxTokens: 2000);
        }

        /// <summary>
        /// Scores how well the candidate's CV matches a recruiter/company (1-10) with reasoning.
        /// </summary>
        public async Task<AIMatchResponse> AnalyzeMatch(Recruiter recruiter, string cvData)
        {
            var result = await CompleteJsonAsync<AIMatchResponse>(MatchSystemPrompt, BuildMatchPrompt(recruiter, cvData), temperature: 0.2, maxTokens: 1000);
            result.Recruiter = recruiter;
            return result;
        }

        /// <summary>
        /// Extracts raw text from a PDF, keeping line breaks so the AI can recognise sections.
        /// </summary>
        private static string ExtractTextFromPdf(Stream pdfStream)
        {
            using var pdf = PdfDocument.Open(pdfStream);
            var builder = new StringBuilder();

            foreach (var page in pdf.GetPages())
            {
                var pageText = ContentOrderTextExtractor.GetText(page);
                pageText = Regex.Replace(pageText, @"[ \t]+", " ");      // collapse horizontal whitespace
                pageText = Regex.Replace(pageText, @"(\r?\n){3,}", "\n\n"); // at most one blank line

                builder.AppendLine(pageText.Trim());
                builder.AppendLine();
            }

            return builder.ToString().Trim();
        }

        /// <summary>
        /// Build prompt for CV extraction and formatting
        /// </summary>
        private static string BuildCVExtractionPrompt(string rawText, string? fileName)
        {
            return $@"Here is the raw text extracted from a CV file{(fileName != null ? $" (filename: {fileName})" : "")}:

{rawText}

Please extract and format this CV information into the following clean, structured format. 
Use the exact structure below, filling in what you can find from the raw text.
If information is missing, omit that section rather than making it up.

SOFTWARE ENGINEER - [Most Relevant Title Based on Experience]

PERSONAL INFORMATION
Name: [Full Name]
Website: [Website/LinkedIn/GitHub if available, otherwise omit]
Location: [City, Country]
Languages: [Languages with proficiency levels, e.g., English (Fluent), Spanish (Native)]

PROFESSIONAL SUMMARY
[2-3 sentence summary of experience, expertise, and career highlights based on the CV]

TECHNICAL SKILLS
• Frontend: [List frontend technologies, frameworks, libraries]
• Backend: [List backend technologies, languages, frameworks]
• Databases: [List databases and data technologies]
• DevOps: [List DevOps tools, cloud platforms, CI/CD]
• Tools: [List development tools, IDEs, project management tools]
• Testing: [List testing frameworks and methodologies]

WORK EXPERIENCE

[COMPANY NAME] ([Start Year] - [End Year/Present])
[Job Title]
• [Achievement/Responsibility 1 with quantifiable results]
• [Achievement/Responsibility 2 with quantifiable results]
• [Achievement/Responsibility 3 with quantifiable results]

[Repeat for each position, most recent first]

OPEN SOURCE CONTRIBUTIONS
• [List significant open source contributions with details]

EDUCATION

[DEGREE] ([Start Year] - [End Year])
[Institution Name]
• [Thesis/Focus if relevant]
• [GPA if impressive]

[Repeat for each degree]

CERTIFICATIONS
• [Certification Name] ([Year])
• [Certification Name] ([Year])

PROJECTS

[PROJECT NAME]
• [Project description and technologies used]
• [Key achievements or metrics]
• [Links if available]

[Repeat for significant projects]

LANGUAGES
• [Language] ([Proficiency])
• [Language] ([Proficiency])

Important formatting rules:
1. Use ALL CAPS for section headers (PERSONAL INFORMATION, TECHNICAL SKILLS, etc.)
2. Use bullet points with • for lists
3. Keep the format clean and consistent
4. Extract quantifiable achievements where possible (e.g., ""Increased performance by 40%"")
5. If you find the person's name in the CV, use it. Otherwise, leave it blank
6. Be accurate - only include information that's actually in the CV
7. If you can't determine the exact job title, use the most appropriate one based on experience
8. For WORK EXPERIENCE, list in reverse chronological order (most recent first)

Output ONLY the formatted CV, no additional text or explanations.";
        }

        private static string BuildMatchPrompt(Recruiter recruiter, string cvData)
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

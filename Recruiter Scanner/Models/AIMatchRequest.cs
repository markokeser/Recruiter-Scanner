namespace Recruiter_Scanner.Models
{
    public class AIMatchRequest
    {
        public int RecruiterIndex { get; set; }
        public Recruiter Recruiter { get; set; }
        public string CVData { get; set; }
    }

    public class AIMatchResponse
    {
        public int Score { get; set; }
        public string Reasoning { get; set; }
        public Recruiter Recruiter { get; set; }
        public string CompanyAnalysis { get; set; }
        public string LocationMatch { get; set; }
        public string IndustryMatch { get; set; }
        public string KeyFindings { get; set; }
        public List<string> Strengths { get; set; } = new List<string>();
        public List<string> Weaknesses { get; set; } = new List<string>();
    }

    public class AISettings
    {
        public string ApiKey { get; set; }
        public string ApiUrl { get; set; }
        public string Model { get; set; }
    }
}

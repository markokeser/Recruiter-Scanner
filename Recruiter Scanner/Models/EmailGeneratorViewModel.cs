using System.Text.Json.Serialization;

namespace Recruiter_Scanner.Models
{
    public class EmailGeneratorViewModel
    {
        public List<MatchData> Matches { get; set; } = new List<MatchData>();
        public string? SelectedCompany { get; set; }
        public bool ShowEmailPanel { get; set; }
    }

    public class MatchData
    {
        public string CompanyName { get; set; }
        public string Website { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string RecruiterName { get; set; }
        public string RecruiterTitle { get; set; }
        public string RecruiterEmail { get; set; }
        public string EmailStatus { get; set; }
        public int? MatchScore { get; set; }
        public string Reasoning { get; set; }
        public string CompanyAnalysis { get; set; }
        public string LocationMatch { get; set; }
        public string IndustryMatch { get; set; }
        public string KeyFindings { get; set; }
        public List<string> Strengths { get; set; } = new List<string>();
        public List<string> Weaknesses { get; set; } = new List<string>();
        public DateTime? AnalyzedAt { get; set; }
        [JsonPropertyName("linkedinProfile")]
        public string RecruiterLinkedIn { get; set; }
        [JsonPropertyName("linkedinCompany")]
        public string CompanyLinkedIn { get; set; }
    }
}

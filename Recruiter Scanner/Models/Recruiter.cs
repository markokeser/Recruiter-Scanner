using System.ComponentModel.DataAnnotations;

namespace Recruiter_Scanner.Models
{
    public class Recruiter
    {
        [Display(Name = "Company Name")]
        public string CompanyNameForEmails { get; set; }

        [Display(Name = "Website")]
        public string Website { get; set; }

        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Display(Name = "LinkedIn URL")]
        public string PersonLinkedinUrl { get; set; }

        [Display(Name = "Title")]
        public string Title { get; set; }

        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Email Status")]
        public string EmailStatus { get; set; }

        [Display(Name = "Corporate Phone")]
        public string CorporatePhone { get; set; }

        [Display(Name = "# Employees")]
        public string NumberOfEmployees { get; set; }

        [Display(Name = "Company LinkedIn")]
        public string CompanyLinkedinUrl { get; set; }

        [Display(Name = "Facebook URL")]
        public string FacebookUrl { get; set; }

        [Display(Name = "Twitter URL")]
        public string TwitterUrl { get; set; }

        [Display(Name = "City")]
        public string City { get; set; }

        [Display(Name = "State")]
        public string State { get; set; }

        [Display(Name = "Country")]
        public string Country { get; set; }

        [Display(Name = "Company Address")]
        public string CompanyAddress { get; set; }

        [Display(Name = "Company City")]
        public string CompanyCity { get; set; }
    }

    public class RecruiterUploadViewModel
    {
        public List<Recruiter> Recruiters { get; set; } = new List<Recruiter>();
        public string ErrorMessage { get; set; }
        public bool ShowResults { get; set; }
    }

    public class JsonRecruiter
    {
        public string companyName { get; set; }
        public string website { get; set; }
        public string city { get; set; }
        public string country { get; set; }
        public string recruiterName { get; set; }
        public string recruiterTitle { get; set; }
        public string recruiterEmail { get; set; }
        public string emailStatus { get; set; }
        public int? matchScore { get; set; }
        public string reasoning { get; set; }
        public string companyAnalysis { get; set; }
        public string locationMatch { get; set; }
        public string industryMatch { get; set; }
        public string keyFindings { get; set; }
        public List<string> strengths { get; set; }
        public List<string> weaknesses { get; set; }
        public string recommendation { get; set; }
        public DateTime analyzedAt { get; set; }
    }
}

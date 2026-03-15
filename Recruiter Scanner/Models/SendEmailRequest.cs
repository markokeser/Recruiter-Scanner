namespace Recruiter_Scanner.Models
{
    public class SendEmailRequest
    {
        public string To { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string RecruiterName { get; set; }
    }
}

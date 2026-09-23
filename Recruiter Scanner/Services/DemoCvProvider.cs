namespace Recruiter_Scanner.Services
{
    /// <summary>
    /// Loads the demo CV (wwwroot/demo/demo_cv.txt), used whenever the user hasn't provided their own.
    /// </summary>
    public class DemoCvProvider
    {
        public string Text { get; }

        public DemoCvProvider(IWebHostEnvironment env)
        {
            Text = File.ReadAllText(Path.Combine(env.WebRootPath, "demo", "demo_cv.txt"));
        }
    }
}

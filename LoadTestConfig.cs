using System.Text.Json;

namespace NBomber_LoadTesting
{  
    public class LoadTestConfig
    {
        public string TargetUrl { get; set; } = "";
        public int DurationMinutes { get; set; }
        public int InitialRequestsPerSecond { get; set; }
        public int MaximumRequestsPerSecond { get; set; }
        public int InitialRateDurationSeconds { get; set; }
        public int RequestTimeoutSeconds { get; set; }


        // Read test settings from JSON file
        public static LoadTestConfig ReadConfig()
        {
            string json = File.ReadAllText("appsettings.json");

            var config = JsonSerializer.Deserialize<LoadTestConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (config == null)
            {
                throw new Exception("Unable to read configuration.");
            }                

            config.Validate();
            return config;
        }

        // Validate config before starting the test
        public void Validate()
        {
            if (!Uri.TryCreate(TargetUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            {
                throw new Exception("Invalid target URL.");
            }                

            if (DurationMinutes <= 0)
            {
                throw new Exception("Duration must be greater than zero.");
            }                

            if (InitialRequestsPerSecond <= 0 || MaximumRequestsPerSecond < InitialRequestsPerSecond || MaximumRequestsPerSecond > 1000)
            {
                throw new Exception("Invalid request rate.");
            }

            if (InitialRateDurationSeconds <= 0 || InitialRateDurationSeconds >= DurationMinutes * 60)
            {
                throw new Exception("Invalid initial load duration.");
            }   

            if (RequestTimeoutSeconds <= 0)
            {
                throw new Exception("Invalid request timeout.");
            }                
        }
    }

}
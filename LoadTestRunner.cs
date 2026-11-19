using NBomber.Contracts;
using NBomber.CSharp;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace NBomber_LoadTesting
{
    public class LoadTestRunner
    {
        private readonly LoadTestConfig _config;
        private readonly HttpClient _client;

        private long _successCount;
        private long _failureCount;

        private readonly ConcurrentBag<double> _responseTimes = new();

        public LoadTestRunner(LoadTestConfig config)
        {
            _config = config;

            // Reuse HttpClient for all API requests
            _client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds)
            };
        }

        // Start NBomber load test
        public void RunTest()
        {
            var totalDuration = TimeSpan.FromMinutes(_config.DurationMinutes);
            var initialDuration = TimeSpan.FromSeconds(_config.InitialRateDurationSeconds);
            var rampDuration = totalDuration - initialDuration;

            Console.WriteLine($"Target URL: {_config.TargetUrl}");
            Console.WriteLine("Starting load test...");

            var scenario = Scenario.Create("API_Load_Test", async context =>
            {
                return await SendRequest();
            })
            .WithLoadSimulations(

                // Send initial number of requests per second
                Simulation.Inject(
                    rate: _config.InitialRequestsPerSecond,
                    interval: TimeSpan.FromSeconds(1),
                    during: initialDuration),

                // Gradually increase requests to maximum rate
                Simulation.RampingInject(
                    rate: _config.MaximumRequestsPerSecond,
                    interval: TimeSpan.FromSeconds(1),
                    during: rampDuration)
            );

            // Execute the scenario
            NBomberRunner
                .RegisterScenarios(scenario)
                .Run();

            // Print final test results
            PrintResults();
        }

        // Send GET request and capture response time
        private async Task<IResponse> SendRequest()
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Send GET request
                using var response = await _client.GetAsync(_config.TargetUrl);

                stopwatch.Stop();

                // Capture response time
                _responseTimes.Add(stopwatch.Elapsed.TotalMilliseconds);

                if (response.IsSuccessStatusCode)
                {
                    Interlocked.Increment(ref _successCount);
                    return Response.Ok();
                }

                Interlocked.Increment(ref _failureCount);
                return Response.Fail(statusCode: ((int)response.StatusCode).ToString(), message: response.StatusCode.ToString());
            }
            catch (TaskCanceledException)
            {
                stopwatch.Stop();
                _responseTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
                Interlocked.Increment(ref _failureCount);
                return Response.Fail(statusCode: "Timeout", message: "Request timed out.");
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                _responseTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
                Interlocked.Increment(ref _failureCount);
                return Response.Fail(statusCode: "RequestError", message: ex.Message);
            }
        }


        // Display load test execution results
        private void PrintResults()
        {
            Console.WriteLine("\n========== LOAD TEST RESULTS ==========");

            Console.WriteLine($"Successful Requests: {_successCount}");
            Console.WriteLine($"Failed Requests: {_failureCount}");
            Console.WriteLine($"Total Requests: {_successCount + _failureCount}");

            if (_responseTimes.IsEmpty)
            {
                Console.WriteLine("No response time data available.");
                return;
            }

            var times = _responseTimes.OrderBy(x => x).ToArray();

            Console.WriteLine("\nResponse Time (ms)");

            Console.WriteLine($"Minimum: {times.First():0.##}");
            Console.WriteLine($"Average: {times.Average():0.##}");
            Console.WriteLine($"Maximum: {times.Last():0.##}");

            Console.WriteLine($"P50: {GetPercentile(times, 50):0.##}");
            Console.WriteLine($"P90: {GetPercentile(times, 90):0.##}");
            Console.WriteLine($"P95: {GetPercentile(times, 95):0.##}");
            Console.WriteLine($"P99: {GetPercentile(times, 99):0.##}");

            Console.WriteLine("=======================================");
        }

        // Calculate response time percentile
        private double GetPercentile(double[] times, int percentile)
        {
            int index = (int)Math.Ceiling((percentile / 100.0) * times.Length) - 1;
            index = Math.Max(0, Math.Min(index, times.Length - 1));
            return times[index];
        }
    }

}

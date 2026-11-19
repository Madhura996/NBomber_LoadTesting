using NBomber_LoadTesting;

// Read configuration
var config = LoadTestConfig.ReadConfig();

// Start load test
var runner = new LoadTestRunner(config);
runner.RunTest();


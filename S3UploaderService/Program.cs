using Microsoft.Extensions.Hosting.WindowsServices;
using S3UploaderService;

var builder = Host.CreateApplicationBuilder(args);

// Ensure it can run as a Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ProblemReporterS3Uploader";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

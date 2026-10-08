using System.Security.Cryptography;
using System.Text;
using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using Amazon.Runtime;

namespace S3UploaderService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private FileSystemWatcher? _watcher;
    private readonly string _watchDirectory;
    private readonly string _bucketName;
    private readonly string _trustAnchorArn;
    private readonly string _profileArn;
    private readonly string _roleArn;
    private readonly string _region;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        // Load configuration
        _watchDirectory = _configuration["S3Uploader:WatchDirectory"] ?? @"C:\ProblemReporter\Output";
        _bucketName = _configuration["S3Uploader:BucketName"] ?? "";
        _trustAnchorArn = _configuration["S3Uploader:TrustAnchorArn"] ?? "";
        _profileArn = _configuration["S3Uploader:ProfileArn"] ?? "";
        _roleArn = _configuration["S3Uploader:RoleArn"] ?? "";
        _region = _configuration["S3Uploader:Region"] ?? "us-east-1";
    }

    private async Task<SessionAWSCredentials?> GetRolesAnywhereCredentialsAsync()
    {
        string certPath = Path.Combine(Path.GetTempPath(), "aide_cert.pem");
        string keyPath = Path.Combine(Path.GetTempPath(), "aide_key.pem");

        try
        {
            var helperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aws_signing_helper.exe");
            if (!File.Exists(helperPath))
            {
                _logger.LogError("aws_signing_helper.exe not found in application directory.");
                return null;
            }

            // Extract cert from LocalMachine store
            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadOnly);
                var certs = store.Certificates.Find(X509FindType.FindBySubjectName, "AIDEEndpoint", false);
                if (certs.Count == 0)
                {
                    _logger.LogError("Could not find AIDEEndpoint certificate in LocalMachine\\My store.");
                    return null;
                }
                var cert = certs[0];
                var certPem = cert.ExportCertificatePem();
                var keyPem = cert.GetRSAPrivateKey()?.ExportRSAPrivateKeyPem();
                if (keyPem == null)
                {
                    _logger.LogError("Could not extract private key from certificate. Is it exportable?");
                    return null;
                }
                await File.WriteAllTextAsync(certPath, certPem);
                await File.WriteAllTextAsync(keyPath, keyPem);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = helperPath,
                Arguments = $"credential-process --certificate \"{certPath}\" --private-key \"{keyPath}\" --trust-anchor-arn {_trustAnchorArn} --profile-arn {_profileArn} --role-arn {_roleArn}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError($"aws_signing_helper failed with exit code {process.ExitCode}. Error: {error}");
                return null;
            }

            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;
            
            return new SessionAWSCredentials(
                root.GetProperty("AccessKeyId").GetString(),
                root.GetProperty("SecretAccessKey").GetString(),
                root.GetProperty("SessionToken").GetString()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error obtaining credentials from IAM Roles Anywhere: {ex.Message}");
            return null;
        }
        finally
        {
            if (File.Exists(certPath)) File.Delete(certPath);
            if (File.Exists(keyPath)) File.Delete(keyPath);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("S3 Uploader Service starting up at: {time}", DateTimeOffset.Now);

        if (string.IsNullOrEmpty(_bucketName) || string.IsNullOrEmpty(_trustAnchorArn) || string.IsNullOrEmpty(_profileArn) || string.IsNullOrEmpty(_roleArn))
        {
            _logger.LogError("AWS Roles Anywhere ARNs or Bucket Name are not configured. Please check appsettings.json. The service will idle until restarted with valid configuration.");
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (TaskCanceledException)
            {
            }
            return;
        }

        if (!Directory.Exists(_watchDirectory))
        {
            _logger.LogWarning($"Watch directory '{_watchDirectory}' does not exist. Creating it.");
            Directory.CreateDirectory(_watchDirectory);
        }

        _watcher = new FileSystemWatcher(_watchDirectory)
        {
            Filter = "*.csv",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _watcher.Created += async (sender, e) => await OnChangedAsync(e.FullPath);
        _watcher.Changed += async (sender, e) => await OnChangedAsync(e.FullPath);

        // Process any files that were already in the directory before the service started
        try
        {
            foreach (var file in Directory.GetFiles(_watchDirectory, "*.csv"))
            {
                _logger.LogInformation($"Processing existing file on startup: {file}");
                await OnChangedAsync(file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error scanning directory on startup: {ex.Message}");
        }

        try
        {
            // Wait indefinitely until the service is stopped
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // Ignore when canceled
        }
    }

    private async Task OnChangedAsync(string filePath)
    {
        _logger.LogInformation($"Detected new or modified file: {filePath}");

        // Add a small delay to ensure the file is completely written by the PowerShell script
        await Task.Delay(2000);

        try
        {
            var credentials = await GetRolesAnywhereCredentialsAsync();
            if (credentials == null)
            {
                _logger.LogError("Could not obtain temporary credentials. Aborting upload.");
                return;
            }

            var regionEndpoint = RegionEndpoint.GetBySystemName(_region);
            var s3Client = new AmazonS3Client(credentials, regionEndpoint);
            var transferUtility = new TransferUtility(s3Client);

            var fileInfo = new FileInfo(filePath);
            var s3Key = $"uploads/{DateTime.UtcNow:yyyy-MM-dd}/{fileInfo.Name}";

            _logger.LogInformation($"Uploading {fileInfo.Name} to S3 bucket {_bucketName} at key {s3Key}");

            await transferUtility.UploadAsync(filePath, _bucketName, s3Key);

            _logger.LogInformation($"Upload successful for {fileInfo.Name}. Deleting local file.");
            
            // Delete the file after successful upload to prevent re-uploading
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error uploading file {filePath}: {ex.Message}");
        }
    }

    public override void Dispose()
    {
        _watcher?.Dispose();
        base.Dispose();
    }
}

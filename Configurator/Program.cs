using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

Console.WriteLine("AWS S3 Uploader Service Configurator");
Console.WriteLine("====================================");
Console.WriteLine($"Running as User: {Environment.UserName}");
Console.WriteLine();
Console.WriteLine("This tool must be run as the 'NT AUTHORITY\\SYSTEM' (LocalSystem) account.");
Console.WriteLine("If you are running as an Administrator, the service will not be able to decrypt the keys.");
Console.WriteLine();

Console.Write("Enter your S3 Bucket Name: ");
string bucketName = Console.ReadLine()?.Trim() ?? "";

Console.Write("Enter your AWS Access Key ID: ");
string accessKey = Console.ReadLine()?.Trim() ?? "";

Console.Write("Enter your AWS Secret Access Key: ");
string secretKey = Console.ReadLine()?.Trim() ?? "";

Console.Write("Enter your AWS Region (default: us-east-1): ");
string region = Console.ReadLine()?.Trim() ?? "";
if (string.IsNullOrEmpty(region)) region = "us-east-1";

if (string.IsNullOrEmpty(bucketName) || string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
{
    Console.WriteLine("Error: Bucket Name, Access Key, and Secret Key are required.");
    return;
}

try
{
    byte[] accessKeyBytes = Encoding.UTF8.GetBytes(accessKey);
    byte[] secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);

    byte[] encryptedAccessKeyBytes = ProtectedData.Protect(accessKeyBytes, null, DataProtectionScope.CurrentUser);
    byte[] encryptedSecretKeyBytes = ProtectedData.Protect(secretKeyBytes, null, DataProtectionScope.CurrentUser);

    string encryptedAccessKeyBase64 = Convert.ToBase64String(encryptedAccessKeyBytes);
    string encryptedSecretKeyBase64 = Convert.ToBase64String(encryptedSecretKeyBytes);

    string appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
    
    if (!File.Exists(appSettingsPath))
    {
        Console.WriteLine($"Error: Cannot find appsettings.json at {appSettingsPath}");
        return;
    }

    string json = File.ReadAllText(appSettingsPath);
    
    // Replace values (works with both empty defaults and populated ones)
    json = Regex.Replace(json, @"""BucketName"":\s*"".*?""", $@"""BucketName"": ""{bucketName}""");
    json = Regex.Replace(json, @"""Region"":\s*"".*?""", $@"""Region"": ""{region}""");
    json = Regex.Replace(json, @"""AccessKeyEncrypted"":\s*"".*?""", $@"""AccessKeyEncrypted"": ""{encryptedAccessKeyBase64}""");
    json = Regex.Replace(json, @"""SecretKeyEncrypted"":\s*"".*?""", $@"""SecretKeyEncrypted"": ""{encryptedSecretKeyBase64}""");

    File.WriteAllText(appSettingsPath, json);
    
    Console.WriteLine("Configuration saved successfully!");
    Console.WriteLine("The service can now be started. Remember to restart it if it is already running.");
}
catch (Exception ex)
{
    Console.WriteLine($"Error during encryption or saving: {ex.Message}");
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();

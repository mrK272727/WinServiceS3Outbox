# AIDE Problem Uploader by Kabul - Deployment Walkthrough

The **AIDE Problem Uploader by Kabul (v3.0)** Windows Service has been significantly upgraded to use **AWS IAM Roles Anywhere**, the enterprise standard for endpoint authentication!

The service no longer stores long-lived IAM keys (encrypted or not). Instead, it uses a Windows Machine Certificate to securely negotiate temporary, short-lived AWS credentials on the fly using the official AWS signing utility.

## 1. Zero-Touch Installation

Because all configuration (the AWS ARNs) is not sensitive, they have been bundled directly into the `appsettings.json` file inside the installer for you, alongside the `aws_signing_helper.exe` binary.

[AIDEs3ProblemUploaderService.msi](file:///C:/Users/mrfue/MTTR_Reducer/S3UploaderService/AIDEs3ProblemUploaderService.msi)

1. Double-click to install the MSI on your machine.
2. The installer will automatically deploy the Service, configuration, and dependencies.
3. The service will start automatically in the background as `LocalSystem`.

## 2. Enterprise Mass Deployment 

This zero-touch architecture is perfectly suited for mass deployment via SCCM, Intune, or any MDM:

1. Deploy the generic MSI to all 1,000+ endpoints silently.
2. Have your MDM/PKI auto-enroll the machines to receive a client certificate (Subject: `CN=AIDEEndpoint`) in their `LocalMachine` Certificate Store.
3. **The Magic**: The service will seamlessly start, dynamically extract the machine certificate from the local store, exchange it for temporary AWS credentials using the baked-in ARNs, and upload files without any human interaction required!

## 3. Advanced Features

> [!TIP]
> **Startup Sweep**: To prevent race conditions where files are generated during a machine reboot or before the service has fully started, version 3.0 performs an immediate directory scan on startup. Any existing `.csv` files found in the output directory will be swept up and uploaded immediately, ensuring zero data loss.

## 4. How to Test It

Assuming your machine has the `AIDEEndpoint` certificate installed (via the guide), testing is instantaneous:

1. Open the Windows Services app (`services.msc`).
2. Verify **Problem Reporter S3 Uploader Service** is running.
3. Drop a test `.csv` file into `C:\ProblemReporter\Output`.
4. Check your AWS S3 bucket in the AWS Console. The file will appear there instantly, uploaded securely using the temporary credentials issued to your machine certificate!

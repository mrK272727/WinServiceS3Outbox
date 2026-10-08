# Comprehensive Guide: AWS IAM Roles Anywhere

This guide details the steps required to configure AWS IAM Roles Anywhere for zero-touch endpoint authentication. By following these steps, you will establish a secure, certificate-based trust relationship between your endpoints and AWS, eliminating the need to deploy long-lived IAM Secret Keys.

## Phase 1: Local Certificate Authority (PKI)

In a production enterprise environment, you will use your existing Microsoft CA or MDM (Intune, SCCM, Workspace ONE) to deploy certificates to endpoints. For this demonstration, we will create a Private Root CA and issue an Endpoint Certificate locally.

Open an **Elevated PowerShell** (Run as Administrator) and execute the following commands exactly as shown:

```powershell
# 1. Create your Private Root CA Certificate
$rootCa = New-SelfSignedCertificate -Subject "CN=AIDEServiceRootCA" -CertStoreLocation "Cert:\LocalMachine\My" -KeyUsage CertSign, CRLSign, DigitalSignature -KeyAlgorithm RSA -KeyLength 2048 -NotAfter (Get-Date).AddYears(1)

# 2. Issue a Client Certificate for the Endpoint (signed by your Root CA)
# Note: KeyExportPolicy must be Exportable so the Windows Service can access it
New-SelfSignedCertificate -Subject "CN=AIDEEndpoint" -CertStoreLocation "Cert:\LocalMachine\My" -Signer $rootCa -KeyExportPolicy Exportable -KeySpec Signature

# 3. Export the Root CA public key to your desktop for AWS Upload
$certPath = "$env:USERPROFILE\Desktop\AIDEServiceRootCA.cer"
Export-Certificate -Cert $rootCa -FilePath $certPath
certutil -f -encode $certPath $certPath > $null

Write-Host "Success! Upload AIDEServiceRootCA.cer from your Desktop to AWS."
```

> [!TIP]
> Ensure there is only **one** certificate named `AIDEEndpoint` in your `LocalMachine\Personal` store. If you ran the script multiple times, open `certlm.msc`, delete the duplicates, and run the script once more.

## Phase 2: AWS Configuration

Navigate to the **IAM Roles Anywhere** console in AWS and complete the following sequence:

### Step 1: Create the Trust Anchor
1. Click **Trust anchors** > **Create a trust anchor**.
2. Name it `AIDEServiceTrustAnchor`.
3. Under Certificate Authority (CA) source, select **External certificate bundle**.
4. Open the `AIDEServiceRootCA.cer` file on your Desktop with Notepad, copy the entire block of text (including `-----BEGIN CERTIFICATE-----`), and paste it into the AWS console.
5. Click **Create trust anchor**.

### Step 2: Create the IAM Role
1. Navigate to the standard **IAM** Dashboard > **Roles** > **Create role**.
2. Select **Custom trust policy** and paste the following highly-secure policy, which ensures *only* the `AIDEEndpoint` certificate can assume this role:

```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Principal": {
                "Service": "rolesanywhere.amazonaws.com"
            },
            "Action": [
                "sts:AssumeRole",
                "sts:TagSession",
                "sts:SetSourceIdentity"
            ],
            "Condition": {
                "StringEquals": {
                    "aws:PrincipalTag/x509Subject/CN": "AIDEEndpoint"
                }
            }
        }
    ]
}
```
3. Click **Next**, and attach the **AmazonS3FullAccess** policy (or a scoped-down S3 policy for your bucket).
4. Name the role `EndpointS3UploaderRole` and click **Create role**.

### Step 3: Create the Profile
1. Return to the **IAM Roles Anywhere** dashboard.
2. Click **Profiles** > **Create a profile**.
3. Name it `AIDES3UploaderProfile`.
4. Under **Roles**, assign the `EndpointS3UploaderRole` you just created.
5. Click **Create profile**.

Your AWS infrastructure is now fully configured to accept certificate-based uploads from your endpoints!

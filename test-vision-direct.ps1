# Hands-on test: calls the deployed Foundry model directly, no playground, no API layer.
# Usage: .\test-vision-direct.ps1 -ImagePath "C:\path\to\photo.jpg"

param(
    [Parameter(Mandatory=$true)]
    [string]$ImagePath
)

# Pull endpoint/key/deployment straight from your local.settings.json so nothing is duplicated/hardcoded.
$settings = Get-Content "$PSScriptRoot\src\famkit.Api\local.settings.json" | ConvertFrom-Json
$endpoint = $settings.Values.'Foundry:Endpoint'.TrimEnd('/')
$apiKey = $settings.Values.'Foundry:ApiKey'
$deployment = $settings.Values.'Foundry:DeploymentName'

$bytes = [System.IO.File]::ReadAllBytes($ImagePath)
$base64 = [Convert]::ToBase64String($bytes)
$ext = [System.IO.Path]::GetExtension($ImagePath).TrimStart('.').ToLower()
$mimeType = if ($ext -eq 'jpg') { 'jpeg' } else { $ext }

$body = @{
    messages = @(
        @{
            role = "user"
            content = @(
                @{ type = "text"; text = "What food items do you see in this image? List each one." }
                @{ type = "image_url"; image_url = @{ url = "data:image/$mimeType;base64,$base64" } }
            )
        }
    )
    max_tokens = 500
} | ConvertTo-Json -Depth 10

$url = "$endpoint/openai/deployments/$deployment/chat/completions?api-version=2024-10-21"

Write-Host "POST $url"
$response = Invoke-RestMethod -Uri $url -Method Post -Headers @{ "api-key" = $apiKey; "Content-Type" = "application/json" } -Body $body

Write-Host "`n--- Model response ---"
Write-Host $response.choices[0].message.content

# Deploy Azure infrastructure for FFXIV Crafting Companion
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory=$true)]
    [string]$Location,

    [string]$Environment = "prod"
)

$ErrorActionPreference = "Stop"

Write-Host "Creating resource group '$ResourceGroup' in '$Location'..."
az group create --name $ResourceGroup --location $Location

Write-Host "Deploying Bicep templates..."
az deployment group create `
    --resource-group $ResourceGroup `
    --template-file "$PSScriptRoot/../main.bicep" `
    --parameters "$PSScriptRoot/../main.bicepparam" `
    --parameters location=$Location environment=$Environment

Write-Host "Deployment complete!"
$outputs = az deployment group show `
    --resource-group $ResourceGroup `
    --name main `
    --query "properties.outputs" `
    --output json | ConvertFrom-Json

Write-Host "API URL: $($outputs.apiUrl.value)"
Write-Host "SQL Server: $($outputs.sqlServerFqdn.value)"

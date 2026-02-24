targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Deployment environment name (e.g. prod, staging).')
param environment string = 'prod'

@description('Administrator login for the SQL Server.')
param sqlAdminLogin string = 'sqladmin'

@secure()
@description('Administrator password for the SQL Server.')
param sqlAdminPassword string

@secure()
@description('Secret key used for JWT token signing.')
param jwtSecretKey string

@description('Container image to deploy.')
param containerImage string = 'ghcr.io/YOUR_GITHUB_USERNAME/ffxiv-crafting-api:latest'

@description('Allowed CORS origins for the API.')
param corsOrigins string = ''

@description('Discord OAuth client ID.')
param discordClientId string = ''

@secure()
@description('Discord OAuth client secret.')
param discordClientSecret string = ''

@description('Discord OAuth redirect URI.')
param discordRedirectUri string = ''

var prefix = 'ffxiv-${environment}'

module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'logAnalytics'
  params: {
    location: location
    workspaceName: '${prefix}-logs'
  }
}

module sqlServer 'modules/sql-server.bicep' = {
  name: 'sqlServer'
  params: {
    location: location
    serverName: '${prefix}-sql'
    databaseName: '${prefix}-db'
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
  }
}

module containerApps 'modules/container-apps.bicep' = {
  name: 'containerApps'
  params: {
    location: location
    envName: '${prefix}-env'
    appName: '${prefix}-api'
    containerImage: containerImage
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    envVars: [
      {
        name: 'ConnectionStrings__DefaultConnection'
        value: sqlServer.outputs.connectionString
      }
      {
        name: 'Jwt__SecretKey'
        value: jwtSecretKey
      }
      {
        name: 'Cors__AllowedOrigins__0'
        value: corsOrigins
      }
      {
        name: 'Discord__ClientId'
        value: discordClientId
      }
      {
        name: 'Discord__ClientSecret'
        value: discordClientSecret
      }
      {
        name: 'Discord__RedirectUri'
        value: discordRedirectUri
      }
      {
        name: 'ASPNETCORE_ENVIRONMENT'
        value: 'Production'
      }
    ]
  }
}

@description('Full HTTPS URL of the deployed API.')
output apiUrl string = containerApps.outputs.appUrl

@description('Fully qualified domain name of the SQL Server.')
output sqlServerFqdn string = sqlServer.outputs.serverFqdn

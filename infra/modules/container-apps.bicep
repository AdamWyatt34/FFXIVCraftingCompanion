@description('Azure region for the Container Apps resources.')
param location string

@description('Name of the Container Apps managed environment.')
param envName string

@description('Name of the Container App.')
param appName string

@description('Container image to deploy.')
param containerImage string

@description('Resource ID of the Log Analytics workspace.')
param logAnalyticsWorkspaceId string

@description('Non-secret environment variables for the container; secrets arrive through the parameters below.')
param envVars array = []

@description('Container registry server (e.g. ghcr.io).')
param registryServer string = 'ghcr.io'

@description('Container registry username.')
param registryUsername string = ''

@secure()
@description('Container registry password or PAT.')
param registryPassword string = ''

@secure()
@description('SQL connection string, exposed to the API as ConnectionStrings__DefaultConnection.')
param sqlConnectionString string = ''

@secure()
@description('JWT signing key, exposed as Jwt__SecretKey.')
param jwtSecretKey string = ''

@secure()
@description('Key that gates /api/admin/*, exposed as Admin__ApiKey; empty keeps the admin endpoints closed.')
param adminApiKey string = ''

@secure()
@description('Discord OAuth client secret, exposed as Discord__ClientSecret.')
param discordClientSecret string = ''

// Secrets live in the app's secret store and reach the container through secretRef variables, so
// the resource (az containerapp show, the portal) and the deployment history carry names, not
// values. A secret is declared only when it has a value; its variable is then absent, which the
// API reads like an empty setting.
var appSecrets = filter(
  [
    { name: 'sql-connection-string', variable: 'ConnectionStrings__DefaultConnection', value: sqlConnectionString }
    { name: 'jwt-secret-key', variable: 'Jwt__SecretKey', value: jwtSecretKey }
    { name: 'admin-api-key', variable: 'Admin__ApiKey', value: adminApiKey }
    { name: 'discord-client-secret', variable: 'Discord__ClientSecret', value: discordClientSecret }
  ],
  secret => !empty(secret.value)
)
var registrySecrets = empty(registryPassword) ? [] : [{ name: 'registry-password', value: registryPassword }]

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: last(split(logAnalyticsWorkspaceId, '/'))
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: envName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
      registries: empty(registryPassword) ? [] : [
        {
          server: registryServer
          username: registryUsername
          passwordSecretRef: 'registry-password'
        }
      ]
      secrets: concat(registrySecrets, map(appSecrets, secret => { name: secret.name, value: secret.value }))
    }
    template: {
      containers: [
        {
          name: appName
          image: containerImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: concat(envVars, map(appSecrets, secret => { name: secret.variable, secretRef: secret.name }))
          probes: [
            {
              // A cold start on 0.25 vCPU gets up to 150 s (10 x 15 s) before the liveness probe
              // starts counting; the platform caps failureThreshold at 10.
              type: 'Startup'
              httpGet: {
                path: '/health/live'
                port: 8080
              }
              periodSeconds: 15
              failureThreshold: 10
              timeoutSeconds: 5
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
              }
              periodSeconds: 30
              timeoutSeconds: 5
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

@description('Fully qualified domain name of the Container App.')
output fqdn string = containerApp.properties.configuration.ingress.fqdn

@description('Full HTTPS URL of the Container App.')
output appUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'

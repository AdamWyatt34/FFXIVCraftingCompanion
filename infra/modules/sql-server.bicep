@description('Azure region for the SQL Server.')
param location string

@description('Name of the SQL Server.')
param serverName string

@description('Name of the SQL database.')
param databaseName string

@description('Administrator login for the SQL Server.')
param adminLogin string

@secure()
@description('Administrator password for the SQL Server.')
param adminPassword string

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    minimalTlsVersion: '1.2'
  }
}

// 0.0.0.0 is the portal's "Allow Azure services and resources to access this server" switch: any
// Azure-hosted client, from any tenant, may reach port 1433 with valid credentials. The Container
// App has no fixed outbound address on the consumption plan, so a tighter rule needs a private
// endpoint or a VNet-integrated environment. Production (deploySql = false) uses a portal-managed
// server; keep it on this rule and nothing broader.
resource firewallRule 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
    autoPauseDelay: 60
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368
    zoneRedundant: false
  }
}

@description('Fully qualified domain name of the SQL Server.')
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName

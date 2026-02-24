@description('Azure region for the Log Analytics workspace.')
param location string

@description('Name of the Log Analytics workspace.')
param workspaceName string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

@description('Resource ID of the Log Analytics workspace.')
output workspaceId string = workspace.id

@description('The resource name.')
param name string

@description('The geo-location where the resource lives.')
param location string

@description('The resource id of log analytics workspace.')
param logAnalyticsWorkspaceResourceId string

resource log 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: last(split(logAnalyticsWorkspaceResourceId, '/'))!
  scope: resourceGroup(split(logAnalyticsWorkspaceResourceId, '/')[2], split(logAnalyticsWorkspaceResourceId, '/')[4])
}

resource cae 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: name
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: log.properties.customerId
        sharedKey: log.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

output id string = cae.id

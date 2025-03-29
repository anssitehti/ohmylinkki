@description('The resource name.')
param name string

@description('The geo-location where the resource lives.')
param location string

@description('Resource Id of the log analytics workspace which the data will be ingested to.')
param logAnalyticsWorkspaceId string

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspaceId
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    DisableLocalAuth: true
  }
}

output id string = appInsights.id
output connectionString string = appInsights.properties.ConnectionString

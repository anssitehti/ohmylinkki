@description('The resource name.')
param name string

@description('The geo-location where the resource lives.')
param location string


@description('The name of the SKU.')
@allowed([
  'PerGB2018'
])
param skuName string = 'PerGB2018'

@description('Number of days data will be retained for.')
param dataRetention int = 30

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: name
  location: location
  properties: {
    sku: {
      name: skuName
    }
    retentionInDays: dataRetention
  }
}


output id string = logAnalyticsWorkspace.id

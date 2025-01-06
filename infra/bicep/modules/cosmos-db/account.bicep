import { SqlDatabase } from './udd-types.bicep'

@description('The resource name.')
param name string

@description('The geo-location where the resource lives.')
param location string

@description('Enable free tier pricing.')
param enableFreeTier bool = true

param totalThroughputLimit int = enableFreeTier ? 1000 : 400

param databases SqlDatabase[]

resource account 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: name
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    enableFreeTier: enableFreeTier
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    capacity: {
      totalThroughputLimit: totalThroughputLimit
    }
  }
}

module accountDatabases 'sql-database.bicep' = [
  for database in databases: {
    name: '${account.name}-sqldb-${database.name}'
    params: {
      name: database.name
      containers: database.containers
      throughput: database.throughput
      accountName: account.name
      dataContributors: database.dataContributors
    }
  }
]


output id string = account.id
output endpoint string = account.properties.documentEndpoint

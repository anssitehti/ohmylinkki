import { Container } from './udd-types.bicep'

@description('Name of the account.')
param accountName string

@description('Name of the database .')
param name string

@description('List of containers to create in the database.')
param containers Container[]

@description('List of identities that have read and write access to the database.')
param dataContributors string[]

resource account 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' existing = {
  name: accountName
}

@description('Request units per second.')
param throughput int

resource sqlDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-11-15' = {
  name: name
  parent: account
  properties: {
    resource: { id: name }
    options: { throughput: throughput }
  }
}

module dbContainers 'container.bicep' = [
  for container in containers: {
    name: '${account.name}-sqldb-${sqlDatabase.name}-${container.name}'
    params: {
      name: container.name
      partitionKeyPath: container.partitionKeyPath
      accountName: account.name
      databaseName: sqlDatabase.name
      defaultTtl: container.defaultTtl
    }
  }
]

module dataContributorRoleAssignment 'data-role-assignment.bicep' = {
  name: '${account.name}-sqldb-${sqlDatabase.name}-data-contributors'
  params: {
    principalIds: dataContributors
    accountName: account.name
    roleName: 'Cosmos DB Built-in Data Contributor'
  }
}

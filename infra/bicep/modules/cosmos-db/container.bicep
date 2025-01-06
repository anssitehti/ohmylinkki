@description('Name of the container.')
param name string

@description('Name of the account.')
param accountName string

@description('Name of the database.')
param databaseName string

@description('The partition key path.')
param partitionKeyPath string

@description('The default time to live.')
param defaultTtl int

resource account 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' existing = {
  name: accountName

  resource database 'sqlDatabases@2024-11-15' existing = {
    name: databaseName
  }
}

resource container 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  name: name
  parent: account::database
  properties: {
    resource: {
      id: name
      partitionKey: {
        paths: [
          partitionKeyPath
        ]
        kind: 'Hash'
      }
      defaultTtl: defaultTtl
    }
  }
}

@description('The IDs of the principals to assign the role to.')
param principalIds array

@description('The name of the role to assign.')
param roleName string

@description('Name of the account.')
param accountName string

resource account 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' existing = {
  name: accountName
}

var builtInRoleNames = {
  'Cosmos DB Built-in Data Reader': '${account.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000001'
  'Cosmos DB Built-in Data Contributor': '${account.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
}

resource roleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = [
  for principalId in principalIds: {
    name: guid(account.name, principalId, roleName)
    parent: account
    properties: {
      roleDefinitionId: builtInRoleNames[roleName]
      principalId: principalId
      scope: account.id
    }
  }
]

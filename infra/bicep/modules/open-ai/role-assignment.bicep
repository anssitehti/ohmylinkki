@description('The IDs of the principals to assign the role to.')
param principalIds array

@description('The name of the role to assign.')
param roleName string

@description('The ID of the resource to assign the role to.')
param resourceId string

resource account 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: last(split(resourceId, '/'))
}

var builtInRoleNames = {
  'Cognitive Services OpenAI User': subscriptionResourceId(
    'Microsoft.Authorization/roleDefinitions',
    '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
  )
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in principalIds: {
    name: guid(account.name, principalId, roleName)
    properties: {
      roleDefinitionId: builtInRoleNames[roleName]
      principalId: principalId
    }
    scope: account
  }
]

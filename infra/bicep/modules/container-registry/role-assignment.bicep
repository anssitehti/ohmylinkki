@description('The IDs of the principals to assign the role to.')
param principalIds array

@description('The name of the role to assign.')
param roleName string

@description('The ID of the resource to assign the role to.')
param resourceId string

resource cr 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: last(split(resourceId, '/'))
}

var builtInRoleNames = {
  AcrPull: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in principalIds: {
    name: guid(cr.name, principalId, roleName)
    properties: {
      roleDefinitionId: builtInRoleNames[roleName]
      principalId: principalId
    }
    scope: cr
  }
]

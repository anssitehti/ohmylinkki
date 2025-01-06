@description('The IDs of the principals to assign the role to.')
param principalIds array

@description('The name of the role to assign.')
param roleName string

@description('The ID of the resource to assign the role to.')
param resourceId string

resource webPubSub 'Microsoft.SignalRService/webPubSub@2024-03-01' existing = {
  name: last(split(resourceId, '/'))
}

var builtInRoleNames = {
  'Web PubSub Service Owner': subscriptionResourceId(
    'Microsoft.Authorization/roleDefinitions',
    '12cf5a90-567b-43ae-8102-96cf46c7d9b4'
  )
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in principalIds: {
    name: guid(webPubSub.name, principalId, roleName)
    properties: {
      roleDefinitionId: builtInRoleNames[roleName]
      principalId: principalId
    }
    scope: webPubSub
  }
]

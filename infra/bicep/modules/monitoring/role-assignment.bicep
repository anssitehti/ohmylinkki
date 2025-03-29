@description('The IDs of the principals to assign the role to.')
param principalIds array

@description('The name of the role to assign.')
param roleName string

@description('The ID of the resource to assign the role to.')
param resourceId string

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: last(split(resourceId, '/'))
}

var builtInRoleNames = {
  'Monitoring Metrics Publisher': subscriptionResourceId(
    'Microsoft.Authorization/roleDefinitions',
    '3913510d-42f4-4e42-8a64-420c390055eb'
  )
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in principalIds: {
    name: guid(appInsights.name, principalId, roleName)
    properties: {
      roleDefinitionId: builtInRoleNames[roleName]
      principalId: principalId
    }
    scope: appInsights
  }
]

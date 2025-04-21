@description('The resource name.')
param name string

@description('The geo-location where the resource lives.')
param location string

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: name
  location: location
}

output name string = userAssignedIdentity.name
output id string = userAssignedIdentity.id
output principalId string = userAssignedIdentity.properties.principalId

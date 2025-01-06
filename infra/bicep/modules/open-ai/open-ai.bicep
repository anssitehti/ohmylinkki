@description('The resource name.')
param name string

@description('The geo-location where the resource lives.')
param location string

@description('The SKU of the resource.')
param sku string = 'S0'

resource account 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: name
  location: location
  kind: 'OpenAI'
  sku: {
    name: sku
  }
  properties: {
    publicNetworkAccess: 'Enabled'
    customSubDomainName: name
    disableLocalAuth: true
  }
}

resource gpt4oMini 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: account
  name: 'gpt4oMini'
  sku: {
    name: 'GlobalStandard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o-mini'
      version: '2024-07-18'
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
    currentCapacity: 10
    raiPolicyName: 'Microsoft.DefaultV2'
  }
}

output id string = account.id

output endpoint string = account.properties.endpoint

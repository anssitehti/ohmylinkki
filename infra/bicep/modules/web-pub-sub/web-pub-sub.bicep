@description('The resource name.')
param name string

@description('The geo-location where the resource lives.')
param location string

@description('The SKU of the resource.')
param sku string = 'Free_F1'

@description('The capacity of the SKU.')
param skuCapacity int = 1

resource webPubSub 'Microsoft.SignalRService/webPubSub@2024-03-01' = {
  name: name
  location: location
  kind: 'WebPubSub'
  sku: {
    name: sku
    capacity: skuCapacity
  }
  properties: {
    disableLocalAuth: true
  }
}


output id string = webPubSub.id

output endpoint string = 'https://${webPubSub.properties.hostName}'

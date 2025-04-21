import { Secret } from './udd-types.bicep'
@description('The resource name.')
param name string

@description('The location where the resource lives.')
param location string

@description('The resource id of container app environment.')
param environmentId string

@description('Bool indicating if app exposes an external http endpoint.')
param ingressExternal bool = true

@description('Target Port in containers for traffic from ingress.')
param targetPort int = 80

@description('Maximum number of container replicas.')
param scaleMaxReplicas int = 1

@description('Minimum number of container replicas.')
param scaleMinReplicas int = 1

@description('The name of the workload profile to use.')
param workloadProfileName string = 'Consumption'

@description('List of container definitions for the Container App.')
param containers array

@description('List of secrets to be mounted in the container.')
param secrets Secret[] = []

@description('The custom domain for the container app.')
param customDomain string = ''

@description('The resource id of the managed certificate.')
param managedEnvironmentManagedCertificateId string = ''

@description('The managed identity to use for pulling images from ACR.')
param acrPullIdentityResurceId string

@description('The url of the ACR registry to pull images from.')
param acrUrl string

resource containerApp 'Microsoft.App/containerApps@2024-10-02-preview' = {
  name: name
  location: location
  identity: {
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${acrPullIdentityResurceId}': {}
    }
  }
  properties: {
    environmentId: environmentId
    configuration: {
      ingress: {
        allowInsecure: false
        external: ingressExternal
        transport: 'auto'
        targetPort: targetPort
        customDomains: !empty(customDomain)
          ? [
              {
                name: customDomain
                certificateId: managedEnvironmentManagedCertificateId
                bindingType: 'SniEnabled'
              }
            ]
          : null
      }
      secrets: secrets
      registries: [
        {
          server: acrUrl
          identity: acrPullIdentityResurceId
        }
      ]
    }
    template: {
      containers: containers
      scale: {
        maxReplicas: scaleMaxReplicas
        minReplicas: scaleMinReplicas
      }
    }
    workloadProfileName: workloadProfileName
  }
}

output id string = containerApp.id
output fqdn string = containerApp.properties.configuration.ingress.fqdn
output principalId string = containerApp.identity.principalId

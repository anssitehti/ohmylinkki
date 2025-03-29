targetScope = 'subscription'

param env string

param solution string

param location string = deployment().location

param nginxProxyImage string

param apiImage string

param uiImage string

@secure()
param walttiUsername string

@secure()
param walttiPassword string

param customDomain string = ''

param managedEnvironmentManagedCertificateId string = ''

resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: 'rg-${solution}-${env}'
  location: location
}

module openAi '../../modules/open-ai/open-ai.bicep' = {
  name: 'openAi'
  params: {
    name: 'oai-${solution}-${env}'
    location: location
  }
  scope: rg
}

module webPubSub '../../modules/web-pub-sub/web-pub-sub.bicep' = {
  name: 'webPubSub'
  params: {
    name: 'wps-${solution}-${env}'
    location: location
  }
  scope: rg
}

module cosmosdb '../../modules/cosmos-db/account.bicep' = {
  name: 'cosmosdb'
  params: {
    name: 'cosmos-${solution}-${env}'
    location: location
    databases: [
      {
        name: 'linkki'
        throughput: 1000
        containers: [
          {
            name: 'locations'
            partitionKeyPath: '/type'
            defaultTtl: -1
            enableSpatialIndexes: true
          }
          {
            name: 'routes'
            partitionKeyPath: '/lineName'
            defaultTtl: -1
            enableSpatialIndexes: false
          }
        ]
      }
    ]
  }
  scope: rg
}

module log '../../modules/monitoring/log-analytics-workspace.bicep' = {
  name: 'log'
  params: {
    name: 'log-${solution}-${env}'
    location: location
  }
  scope: rg
}

module appInsights '../../modules/monitoring/application-insights.bicep' = {
  name: 'appInsights'
  params: {
    name: 'appi-${solution}-${env}'
    location: location
    logAnalyticsWorkspaceId: log.outputs.id
  }
  scope: rg
}

module cae '../../modules/container-app/cae.bicep' = {
  name: 'cae'
  params: {
    name: 'cae-${solution}-${env}'
    location: location
    logAnalyticsWorkspaceResourceId: log.outputs.id
  }
  scope: rg
}

module ui '../../modules/container-app/app.bicep' = {
  name: 'ui'
  params: {
    name: 'ca-${solution}-ui-${env}'
    location: location
    environmentId: cae.outputs.id
    ingressExternal: false
    targetPort: 80
    containers: [
      {
        image: uiImage
        name: 'ohmylinkki-ui'
        resources: {
          cpu: json('0.25')
          memory: '0.5Gi'
        }
      }
    ]
  }
  scope: rg
}

module api '../../modules/container-app/app.bicep' = {
  name: 'api'
  params: {
    name: 'ca-${solution}-api-${env}'
    location: location
    environmentId: cae.outputs.id
    ingressExternal: false
    targetPort: 8080
    secrets: [
      {
        name: 'waltti-username'
        value: walttiUsername
      }
      {
        name: 'waltti-password'
        value: walttiPassword
      }
    ]
    containers: [
      {
        image: apiImage
        name: 'ohmylinkki-api'
        resources: {
          cpu: json('0.25')
          memory: '0.5Gi'
        }
        env: [
          { name: 'OpenAi__Endpoint', value: openAi.outputs.endpoint }
          { name: 'CosmosDb__Endpoint', value: cosmosdb.outputs.endpoint }
          { name: 'WebPubSub__Endpoint', value: webPubSub.outputs.endpoint }
          { name: 'LinkkiImport__WalttiUsername', secretRef: 'waltti-username' }
          { name: 'LinkkiImport__WalttiPassword', secretRef: 'waltti-password' }
        ]
      }
    ]
  }
  scope: rg
}

module nginxProxy '../../modules/container-app/app.bicep' = {
  name: 'nginxProxy'
  params: {
    name: 'ca-${solution}-nginx-proxy-${env}'
    location: location
    environmentId: cae.outputs.id
    ingressExternal: true
    targetPort: 80
    customDomain: customDomain
    managedEnvironmentManagedCertificateId: managedEnvironmentManagedCertificateId
    containers: [
      {
        image: nginxProxyImage
        name: 'nginx-proxy'
        resources: {
          cpu: json('0.25')
          memory: '0.5Gi'
        }
        env: [
          { name: 'API_URL', value: 'https://${api.outputs.fqdn}' }
          { name: 'UI_URL', value: 'https://${ui.outputs.fqdn}' }
        ]
      }
    ]
  }
  scope: rg
}

// Assign roles

module webPubSubContributorRoleAssignment '../../modules/web-pub-sub/role-assignment.bicep' = {
  name: 'webPubSubContributorRoleAssignment'
  params: {
    principalIds: [deployer().objectId, api.outputs.principalId]
    resourceId: webPubSub.outputs.id
    roleName: 'Web PubSub Service Owner'
  }
  scope: rg
}

module openAiUserRoleAssignment '../../modules/open-ai/role-assignment.bicep' = {
  name: 'openAiUserRoleAssignment'
  params: {
    principalIds: [deployer().objectId, api.outputs.principalId]
    resourceId: openAi.outputs.id
    roleName: 'Cognitive Services OpenAI User'
  }
  scope: rg
}

module cosmosdbDataContributorRoleAssignment '../../modules/cosmos-db/data-role-assignment.bicep' = {
  name: 'cosmosdbDataContributorRoleAssignment'
  params: {
    principalIds: [deployer().objectId, api.outputs.principalId]
    accountName: cosmosdb.outputs.name
    roleName: 'Cosmos DB Built-in Data Contributor'
  }
  scope: rg
}

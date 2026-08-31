param name string
param location string = resourceGroup().location
param tags object = {}

@description('Principal ID of the proxy user-assigned managed identity for ACR pull')
param proxyPrincipalId string

@description('Resource ID of the Log Analytics workspace that receives model usage logs and metrics.')
param logAnalyticsWorkspaceId string = ''

@description('Explicit name for the AI Foundry account. Defaults to the hash-based "<prefix>-aifoundry" convention when empty. Must be globally unique because it also becomes the custom subdomain.')
param accountName string = ''

var foundryName = empty(accountName) ? '${name}-aifoundry' : accountName

// AI Foundry account
resource aiServices 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' = {
  name: foundryName
  location: location
  tags: tags
  kind: 'AIServices'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    customSubDomainName: foundryName
    publicNetworkAccess: 'Enabled'
    allowProjectManagement: true
  }
  sku: {
    name: 'S0'
  }
}

// AI Foundry Project (child of AI Services — visible in ai.azure.com)
resource aiProject 'Microsoft.CognitiveServices/accounts/projects@2025-04-01-preview' = {
  parent: aiServices
  name: '${foundryName}-project'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {}
}

// Send per-request model usage (token counts per deployment) to Log Analytics.
// Without this only aggregate platform metrics are retained, which cannot be broken down per call.
resource foundryDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (!empty(logAnalyticsWorkspaceId)) {
  name: 'foundry-usage'
  scope: aiServices
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      { category: 'AzureOpenAIRequestUsage', enabled: true }
      { category: 'RequestResponse', enabled: true }
      { category: 'Audit', enabled: true }
    ]
    metrics: [
      { category: 'AllMetrics', enabled: true }
    ]
  }
}

output aiServicesName string = aiServices.name
output aiServicesEndpoint string = aiServices.properties.endpoint
output aiProjectName string = aiProject.name

// Grant the proxy's user-assigned managed identity "Cognitive Services OpenAI User" on the AI Services account
// Role definition ID: 5e0bd9bd-7b93-4f28-af87-19fc36ad61bd
resource proxyOpenAIRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiServices.id, proxyPrincipalId, '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
  scope: aiServices
  properties: {
    principalId: proxyPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
    principalType: 'ServicePrincipal'
  }
}

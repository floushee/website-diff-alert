var websiteStoreContainerName = 'websitestore'

resource storageaccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: 'websitediffstorage'
  location: resourceGroup().location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
}

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-04-01' = {
  name: format('{0}/default/{1}', storageaccount.name, websiteStoreContainerName)
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'website-diff-alert-app-insights'
  location: resourceGroup().location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

resource servicePlan 'Microsoft.Web/serverfarms@2021-01-15' = {
  name: 'website-diff-alert-serviceplan'
  location: resourceGroup().location
  sku: {
    name: 'S1' 
  }
}

resource function 'Microsoft.Web/sites@2021-01-15' = {
  name: 'website-diff-alert-function-app'
  location: resourceGroup().location
  kind: 'functionapp'
  dependsOn: [
    servicePlan
    appInsights
    storageaccount
  ]
  properties: {
    serverFarmId: servicePlan.id
    siteConfig: {
      alwaysOn: true
      appSettings: [
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=websitediffstorage;AccountKey=${listkeys(storageaccount.id, '2021-04-01').keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'WEBSITE_STORE_CONNECTION_STRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=websitediffstorage;AccountKey=${listkeys(storageaccount.id, '2021-04-01').keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'WEBSITE_STORE_CONTAINER'
          value: websiteStoreContainerName
        }
      ]
    }
  }
}


output connectionString string  = 'DefaultEndpointsProtocol=https;AccountName=websitediffstorage;AccountKey=${listkeys(storageaccount.id, '2021-04-01').keys[0].value};EndpointSuffix=core.windows.net'

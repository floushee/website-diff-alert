resource storageaccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: 'websitediffstorage'
  location: resourceGroup().location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
}

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-04-01' = {
  name: format('{0}/default/{1}', storageaccount.name, 'htmlcontainer')
}

output connectionString string  = 'DefaultEndpointsProtocol=https;AccountName=websitediffstorage;AccountKey=${listkeys(storageaccount.id, '2021-04-01').keys[0].value};EndpointSuffix=core.windows.net'

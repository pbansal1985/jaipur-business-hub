param location string = resourceGroup().location
param webAppName string
param apiAppName string
param sqlServerName string
param sqlDatabaseName string
param webOrigin string
@secure()
param sqlAdminPassword string
@secure()
param jwtSigningKey string
@secure()
param adminBootstrapSecret string
param webImage string
param apiImage string

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'jaipur-hub-plan'
  location: location
  sku: {
    name: 'P1v3'
    tier: 'PremiumV3'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource server 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: 'hubadmin'
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: server
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'S2'
    tier: 'Standard'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

resource api 'Microsoft.Web/sites@2023-12-01' = {
  name: apiAppName
  location: location
  kind: 'app,linux,container'
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|' + apiImage
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'Jwt__SigningKey'
          value: jwtSigningKey
        }
        {
          name: 'Jwt__Issuer'
          value: 'JaipurBusinessHub'
        }
        {
          name: 'Jwt__Audience'
          value: 'JaipurBusinessHub.Web'
        }
        {
          name: 'Admin__BootstrapSecret'
          value: adminBootstrapSecret
        }
        {
          name: 'Web__Origins__0'
          value: webOrigin
        }
        {
          name: 'ConnectionStrings__JaipurHub'
          value: 'Server=tcp:' + sqlServerName + '.database.windows.net,1433;Initial Catalog=' + sqlDatabaseName + ';Persist Security Info=False;User ID=hubadmin;Password=' + sqlAdminPassword + ';Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
        }
      ]
    }
  }
}

resource web 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux,container'
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|' + webImage
      appSettings: [
        {
          name: 'NEXT_PUBLIC_API_URL'
          value: 'https://' + apiAppName + '.azurewebsites.net'
        }
        {
          name: 'SITE_URL'
          value: webOrigin
        }
      ]
    }
  }
}

output apiUrl string = 'https://' + apiAppName + '.azurewebsites.net'
output webUrl string = 'https://' + webAppName + '.azurewebsites.net'

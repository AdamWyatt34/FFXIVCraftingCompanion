using './main.bicep'

param environment = 'prod'
param deploySql = false
param sqlAdminLogin = 'sqladmin'
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', '')
param sqlConnectionString = readEnvironmentVariable('SQL_CONNECTION_STRING', '')
param jwtSecretKey = readEnvironmentVariable('JWT_SECRET_KEY', '')
param adminApiKey = readEnvironmentVariable('ADMIN_API_KEY', '')
param containerImage = readEnvironmentVariable('CONTAINER_IMAGE', 'ghcr.io/adamwyatt34/ffxiv-crafting-api:latest')
param corsOrigins = readEnvironmentVariable('CORS_ORIGINS', '')
param discordClientId = readEnvironmentVariable('DISCORD_CLIENT_ID', '')
param discordClientSecret = readEnvironmentVariable('DISCORD_CLIENT_SECRET', '')
param discordRedirectUri = readEnvironmentVariable('DISCORD_REDIRECT_URI', '')
param registryUsername = readEnvironmentVariable('GHCR_USERNAME', '')
param registryPassword = readEnvironmentVariable('GHCR_PASSWORD', '')

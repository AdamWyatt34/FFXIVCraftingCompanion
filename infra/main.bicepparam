using './main.bicep'

param environment = 'prod'
param sqlAdminLogin = 'sqladmin'
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', '')
param jwtSecretKey = readEnvironmentVariable('JWT_SECRET_KEY', '')
param containerImage = readEnvironmentVariable('CONTAINER_IMAGE', 'ghcr.io/YOUR_GITHUB_USERNAME/ffxiv-crafting-api:latest')
param corsOrigins = readEnvironmentVariable('CORS_ORIGINS', '')
param discordClientId = readEnvironmentVariable('DISCORD_CLIENT_ID', '')
param discordClientSecret = readEnvironmentVariable('DISCORD_CLIENT_SECRET', '')
param discordRedirectUri = readEnvironmentVariable('DISCORD_REDIRECT_URI', '')

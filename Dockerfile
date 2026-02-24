# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy solution file and project files for restore
COPY FFXIVCraftingCompanion.slnx ./
COPY src/FFXIV.Domain/FFXIV.Domain.csproj src/FFXIV.Domain/
COPY src/FFXIV.Application/FFXIV.Application.csproj src/FFXIV.Application/
COPY src/FFXIV.Infrastructure/FFXIV.Infrastructure.csproj src/FFXIV.Infrastructure/
COPY src/FFXIV.Api/FFXIV.Api.csproj src/FFXIV.Api/

# Restore dependencies
RUN dotnet restore FFXIVCraftingCompanion.slnx

# Copy remaining source code
COPY src/ src/

# Publish the API project
RUN dotnet publish src/FFXIV.Api/FFXIV.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Install ICU libs for full globalization support
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Expose port and configure URLs
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Create and switch to non-root user
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

# Copy published output from build stage
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "FFXIV.Api.dll"]

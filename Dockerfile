# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy project files for restore (not the solution — tests/ is dockerignored)
# The shared build settings (analyzers, warnings as errors) apply inside the image build too.
COPY Directory.Build.props .editorconfig ./
COPY src/FFXIV.Domain/FFXIV.Domain.csproj src/FFXIV.Domain/
COPY src/FFXIV.Application/FFXIV.Application.csproj src/FFXIV.Application/
COPY src/FFXIV.Infrastructure/FFXIV.Infrastructure.csproj src/FFXIV.Infrastructure/
COPY src/FFXIV.Api/FFXIV.Api.csproj src/FFXIV.Api/

# Restore dependencies for the API project (resolves entire dependency graph)
RUN dotnet restore src/FFXIV.Api/FFXIV.Api.csproj

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

# Create the non-root user, copy the published output as that user, then drop privileges
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
COPY --from=build --chown=appuser:appgroup /app/publish .
USER appuser

ENTRYPOINT ["dotnet", "FFXIV.Api.dll"]

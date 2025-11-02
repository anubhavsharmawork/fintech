# Unified Dockerfile to build any .NET project in this repo using a build arg
# Usage (Heroku heroku.yml): pass PROJECT_PATH=<ProjectFolder>/<Project>.csproj

# --------------------
# Build UI (optional)
# --------------------
FROM node:18 AS ui-build
WORKDIR /ui
# Install deps (use ci if lockfile exists)
COPY ui/package.json ui/package-lock.json* ./
RUN npm ci || npm install
# Copy source and build
COPY ui/ .
RUN npm run build

# --------------------
# Build .NET project
# --------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy everything to allow cross-project restores
COPY . .

# Select which project to build/publish
ARG PROJECT_PATH

# Restore and publish the selected project
RUN dotnet restore ${PROJECT_PATH}
RUN dotnet publish ${PROJECT_PATH} -c Release -o /app/publish /p:UseAppHost=false

# --------------------
# Runtime image (single stack: ASP.NET Core)
# --------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser

WORKDIR /app

# App binaries
COPY --from=build --chown=appuser:appuser /app/publish .

# Copy UI static files into wwwroot (if build exists). This is safe even if not used by the selected project.
COPY --from=ui-build --chown=appuser:appuser /ui/build ./wwwroot

# Switch to non-root user
USER appuser

# Heroku will expose a PORT env var; we don't hardcode ASPNETCORE_URLS here.
ENTRYPOINT ["dotnet"]

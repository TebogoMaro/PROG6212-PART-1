# syntax=docker/dockerfile:1

# ---------------------------------------------------------------
# Stage 1 - build: the full .NET SDK compiles and publishes the app
# ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore packages first, in their own layer: Docker re-uses this layer
# (and skips the slow download) until the .csproj file changes.
COPY RaceDay.Web.csproj ./
RUN dotnet restore RaceDay.Web.csproj

# Now copy the rest of the source and publish a Release build.
COPY . .
RUN dotnet publish RaceDay.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------------------------------------------------------------
# Stage 2 - runtime: a small image with only the ASP.NET Core runtime.
# The SDK, source code and build tools are NOT included in the final image.
# ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The web app listens on port 8080 inside the container.
# ApiBaseUrl tells it where the RaceDay API is; override it with: docker run -e ApiBaseUrl=...
# (host.docker.internal = "the computer Docker is running on", where the API runs during development).
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ApiBaseUrl=http://host.docker.internal:5080
EXPOSE 8080

# Run as the non-root "app" user that ships with the .NET 8 images (safer than root).
USER $APP_UID

ENTRYPOINT ["dotnet", "RaceDay.Web.dll"]

# Local / docker-compose build — framework-dependent, aspnet runtime included.
# For production k8s AOT build use Dockerfile.k8s.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/VitalStream.Ingest/VitalStream.Ingest.csproj",   "src/VitalStream.Ingest/"]
COPY ["src/VitalStream.Shared/VitalStream.Shared.csproj",   "src/VitalStream.Shared/"]
RUN dotnet restore "src/VitalStream.Ingest/VitalStream.Ingest.csproj"

COPY . .
WORKDIR "/src/src/VitalStream.Ingest"
RUN dotnet publish "VitalStream.Ingest.csproj" \
    -c $BUILD_CONFIGURATION \
    /p:PublishAot=false \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "VitalStream.Ingest.dll"]
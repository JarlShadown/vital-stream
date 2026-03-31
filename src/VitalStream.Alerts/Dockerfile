FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/VitalStream.Alerts/VitalStream.Alerts.csproj", "src/VitalStream.Alerts/"]
RUN dotnet restore "src/VitalStream.Alerts/VitalStream.Alerts.csproj"
COPY . .
WORKDIR "/src/src/VitalStream.Alerts"
RUN dotnet build "./VitalStream.Alerts.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./VitalStream.Alerts.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VitalStream.Alerts.dll"]

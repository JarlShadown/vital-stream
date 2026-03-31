FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/VitalStream.Aggregator/VitalStream.Aggregator.csproj", "src/VitalStream.Aggregator/"]
RUN dotnet restore "src/VitalStream.Aggregator/VitalStream.Aggregator.csproj"
COPY . .
WORKDIR "/src/src/VitalStream.Aggregator"
RUN dotnet build "./VitalStream.Aggregator.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./VitalStream.Aggregator.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VitalStream.Aggregator.dll"]

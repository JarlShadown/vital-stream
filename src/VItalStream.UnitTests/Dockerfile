FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/VItalStream.UnitTests/VItalStream.UnitTests.csproj", "src/VItalStream.UnitTests/"]
COPY ["src/VitalStream.Shared/VitalStream.Shared.csproj", "src/VitalStream.Shared/"]
RUN dotnet restore "src/VItalStream.UnitTests/VItalStream.UnitTests.csproj"
COPY . .
WORKDIR "/src/src/VItalStream.UnitTests"
RUN dotnet build "./VItalStream.UnitTests.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./VItalStream.UnitTests.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VItalStream.UnitTests.dll"]

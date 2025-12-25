FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["CodexzierDirectSight/CodexzierDirectSight.csproj", "CodexzierDirectSight/"]
RUN dotnet restore "CodexzierDirectSight/CodexzierDirectSight.csproj"
COPY . .
WORKDIR "/src/CodexzierDirectSight"
RUN dotnet build "./CodexzierDirectSight.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CodexzierDirectSight.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CodexzierDirectSight.dll"]

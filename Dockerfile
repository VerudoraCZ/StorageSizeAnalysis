FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["StorageSizeAnalysis/StorageSizeAnalysis.csproj", "StorageSizeAnalysis/"]
RUN dotnet restore "StorageSizeAnalysis/StorageSizeAnalysis.csproj"
COPY . .
WORKDIR "/src/StorageSizeAnalysis"
RUN dotnet build "StorageSizeAnalysis.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "StorageSizeAnalysis.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "StorageSizeAnalysis.dll"]

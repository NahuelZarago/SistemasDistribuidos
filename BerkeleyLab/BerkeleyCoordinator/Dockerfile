FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BerkeleyCoordinator.csproj ./
RUN dotnet restore BerkeleyCoordinator.csproj
COPY . ./
RUN dotnet publish BerkeleyCoordinator.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BerkeleyCoordinator.dll"]
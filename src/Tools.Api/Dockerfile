FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY LabIAMultiAgents.slnx .
COPY src/Orchestrator.Api/Orchestrator.Api.csproj src/Orchestrator.Api/
COPY src/Tools.Api/Tools.Api.csproj src/Tools.Api/
RUN dotnet restore src/Tools.Api/Tools.Api.csproj

COPY src/Tools.Api/ src/Tools.Api/
RUN dotnet publish src/Tools.Api/Tools.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Tools.Api.dll"]

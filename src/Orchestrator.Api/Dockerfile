FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Orchestrator.Api/Agent.Auto.Orchestor.csproj src/Orchestrator.Api/
RUN dotnet restore src/Orchestrator.Api/Agent.Auto.Orchestor.csproj

COPY src/Orchestrator.Api/ src/Orchestrator.Api/
RUN dotnet publish src/Orchestrator.Api/Agent.Auto.Orchestor.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Agent.Auto.Orchestor.dll"]

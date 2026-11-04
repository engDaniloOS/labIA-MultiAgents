FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Agent.Auto.Web/Agent.Auto.Web.csproj Agent.Auto.Web/
RUN dotnet restore Agent.Auto.Web/Agent.Auto.Web.csproj

COPY Agent.Auto.Web/ Agent.Auto.Web/
RUN dotnet publish Agent.Auto.Web/Agent.Auto.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Agent.Auto.Web.dll"]

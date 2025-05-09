FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ../ ./

RUN dotnet restore PlanningPoker.sln

RUN dotnet publish PlanningPoker.Api/PlanningPoker.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 80

ENTRYPOINT ["dotnet", "PlanningPoker.Api.dll"]
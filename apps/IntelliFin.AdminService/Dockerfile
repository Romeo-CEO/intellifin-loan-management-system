# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["apps/IntelliFin.AdminService/IntelliFin.AdminService.csproj", "apps/IntelliFin.AdminService/"]
RUN dotnet restore "apps/IntelliFin.AdminService/IntelliFin.AdminService.csproj"
COPY . .
WORKDIR "/src/apps/IntelliFin.AdminService"
RUN dotnet publish "IntelliFin.AdminService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IntelliFin.AdminService.dll"]

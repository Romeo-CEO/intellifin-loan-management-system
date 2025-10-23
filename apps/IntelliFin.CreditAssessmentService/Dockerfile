# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src
COPY IntelliFin.sln ./
COPY apps/IntelliFin.CreditAssessmentService/IntelliFin.CreditAssessmentService.csproj apps/IntelliFin.CreditAssessmentService/
COPY libs/IntelliFin.Shared.DomainModels/IntelliFin.Shared.DomainModels.csproj libs/IntelliFin.Shared.DomainModels/
COPY libs/IntelliFin.Shared.Observability/IntelliFin.Shared.Observability.csproj libs/IntelliFin.Shared.Observability/
RUN dotnet restore apps/IntelliFin.CreditAssessmentService/IntelliFin.CreditAssessmentService.csproj

FROM restore AS build
COPY . .
RUN dotnet build apps/IntelliFin.CreditAssessmentService/IntelliFin.CreditAssessmentService.csproj -c Release -o /app/build --no-restore

FROM build AS test
RUN dotnet test IntelliFin.sln -c Release --no-build --logger "trx;LogFileName=test-results.trx"

FROM build AS publish
RUN dotnet publish apps/IntelliFin.CreditAssessmentService/IntelliFin.CreditAssessmentService.csproj -c Release -o /app/publish --no-build /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

RUN useradd --create-home --uid 10001 appuser
USER appuser

COPY --from=publish /app/publish ./

ENTRYPOINT ["dotnet", "IntelliFin.CreditAssessmentService.dll"]

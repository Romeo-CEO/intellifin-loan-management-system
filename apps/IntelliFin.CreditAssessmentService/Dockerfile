# syntax=docker/dockerfile:1

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["global.json", "./"]
COPY ["IntelliFin.sln", "./"]
COPY ["apps/IntelliFin.CreditAssessmentService/IntelliFin.CreditAssessmentService.csproj", "apps/IntelliFin.CreditAssessmentService/"]
COPY ["libs/IntelliFin.Shared.DomainModels/IntelliFin.Shared.DomainModels.csproj", "libs/IntelliFin.Shared.DomainModels/"]
COPY ["libs/IntelliFin.Shared.Observability/IntelliFin.Shared.Observability.csproj", "libs/IntelliFin.Shared.Observability/"]
COPY ["libs/IntelliFin.Shared.Infrastructure/IntelliFin.Shared.Infrastructure.csproj", "libs/IntelliFin.Shared.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "apps/IntelliFin.CreditAssessmentService/IntelliFin.CreditAssessmentService.csproj"

# Copy source code
COPY . .

# Build and publish
WORKDIR "/src/apps/IntelliFin.CreditAssessmentService"
RUN dotnet build "IntelliFin.CreditAssessmentService.csproj" -c Release -o /app/build
RUN dotnet publish "IntelliFin.CreditAssessmentService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Test stage (optional - can be run separately)
FROM build AS test
WORKDIR /src/tests
RUN echo "Tests will be added in Story 1.18"

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN addgroup --system --gid 1001 intellifin && \
    adduser --system --uid 1001 --ingroup intellifin intellifin

# Copy published application
COPY --from=build --chown=intellifin:intellifin /app/publish .

# Switch to non-root user
USER intellifin

# Expose port 8080 (non-privileged port)
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:8080/health/live || exit 1

# Entry point
ENTRYPOINT ["dotnet", "IntelliFin.CreditAssessmentService.dll"]

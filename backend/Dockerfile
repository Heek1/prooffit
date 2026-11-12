FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project file alone, so the layer is reused until dependencies change.
COPY src/ProofFit.Api/ProofFit.Api.csproj src/ProofFit.Api/
RUN dotnet restore src/ProofFit.Api/ProofFit.Api.csproj

COPY src/ProofFit.Api/ src/ProofFit.Api/
RUN dotnet publish src/ProofFit.Api/ProofFit.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# The base image ships a non-root user; the API has no reason to run as root.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ProofFit.Api.dll"]

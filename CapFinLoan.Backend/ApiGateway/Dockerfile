# Build context: CapFinLoan.Backend/
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ApiGateway/CapFinLoan.Gateway.API/CapFinLoan.Gateway.API.csproj \
     ApiGateway/CapFinLoan.Gateway.API/

RUN dotnet restore ApiGateway/CapFinLoan.Gateway.API/CapFinLoan.Gateway.API.csproj

COPY . .
RUN dotnet publish ApiGateway/CapFinLoan.Gateway.API/CapFinLoan.Gateway.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends wget && rm -rf /var/lib/apt/lists/*

RUN groupadd --system appgroup && useradd --system --gid appgroup appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CapFinLoan.Gateway.API.dll"]

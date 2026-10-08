## we need this dockerfile to build the application for production
## in this case , we will build the application that is based on c# and install related libraries
## to run the application using docker-compose up 

## in this case , we are also making a port forward from nginx to project actual localhost port 



# 1. Build Stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["PaymentGateway.Api.csproj", "./"]
RUN dotnet restore "./PaymentGateway.Api.csproj"

# Copy source code and publish in Release mode
COPY . .
RUN dotnet publish "./PaymentGateway.Api.csproj" -c Release -o /app/publish \
    --no-restore /p:UseAppHost=false

# 2. Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
WORKDIR /app

# Install native SkiaSharp dependencies and fonts for QuestPDF
RUN apk add --no-cache \
    fontconfig \
    ttf-dejavu \
    libstdc++

USER $APP_UID
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PaymentGateway.Api.dll"]
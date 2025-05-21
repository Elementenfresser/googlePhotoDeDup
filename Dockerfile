# Use the official .NET runtime image for ASP.NET Core
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

# Copy published output from build stage
COPY publish/ ./

# Copy Google API credentials into the image
COPY client_secret.json ./client_secret.json

# Expose port 80 for the web app
EXPOSE 80

# Set environment variables for ASP.NET Core
ENV ASPNETCORE_URLS=http://+:80

# Start the app
ENTRYPOINT ["dotnet", "photoManager.dll"]

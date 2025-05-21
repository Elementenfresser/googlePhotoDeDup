# PhotoManager

A C# .NET console application that connects to the Google Photos API using OAuth, scans for similar (burst) shots in a given time frame, and provides online links to the photos for review.

## Features
- Google Photos API integration with OAuth
- Scan for similar/burst shots within a specified time frame
- Output direct links to photos for review and further actions

## Getting Started
1. Register your app in the [Google Cloud Console](https://console.developers.google.com/) and enable the Google Photos Library API.
2. Download your OAuth client credentials (client_secret.json) and place it in the project directory.
3. Build and run the application:
   ```pwsh
   dotnet run
   ```
4. Follow the on-screen instructions to authenticate and use the app.

## Requirements
- .NET 6.0 or later
- Google.Apis.PhotosLibrary.v1 NuGet package

## License
MIT

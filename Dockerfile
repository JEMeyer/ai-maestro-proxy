# Stage 1: Build the .NET application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out

# List the files in the out directory for debugging purposes
RUN ls -la out

# Stage 2: Create a minimal image for the final executable
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy the build output from the previous stage
COPY --from=build /app/out .

# Expose port 8080 to the outside world
EXPOSE 8080

# Debugging entrypoint to print environment variables and run the app
ENTRYPOINT ["dotnet", "ai-maestro-proxy.dll"]

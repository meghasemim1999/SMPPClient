# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM docker.resaa.net/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080


# This stage is used to build the service project
FROM docker.resaa.net/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["./README.md", "."]
COPY ["Source/SmppApi/SmppApi.csproj", "Source/SmppApi/"]
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
	dotnet restore "./Source/SmppApi/SmppApi.csproj"
COPY . .
WORKDIR "/src/Source/SmppApi"
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
	dotnet build "./SmppApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
	dotnet publish "./SmppApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmppApi.dll"]
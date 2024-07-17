# Use the official .NET Core SDK image to build the application


FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env


WORKDIR /App





# Copy everything and build


COPY . ./


RUN dotnet restore


RUN dotnet publish -c Release -o /App/publish


# Use the official .NET Core runtime image


FROM mcr.microsoft.com/dotnet/aspnet:8.0


WORKDIR /App


COPY --from=build-env /App/publish .


# Expose port 80


EXPOSE 80


EXPOSE 8080

EXPOSE 443

# Entry point

ENTRYPOINT ["dotnet", "App.dll"]
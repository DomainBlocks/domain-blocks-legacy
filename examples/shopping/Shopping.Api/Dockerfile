FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /
COPY /examples/shopping /examples/shopping
COPY /src /src
COPY /third_party /third_party
WORKDIR "/examples/shopping/Shopping.Api"
RUN dotnet build "Shopping.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Shopping.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Shopping.Api.dll"]

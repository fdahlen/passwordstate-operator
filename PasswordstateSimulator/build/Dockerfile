FROM mcr.microsoft.com/dotnet/sdk:5.0 as build

RUN mkdir /src
WORKDIR /src
COPY . /src

RUN dotnet publish --configuration Release -o out PasswordstateSimulator/PasswordstateSimulator.csproj

FROM mcr.microsoft.com/dotnet/aspnet:5.0

WORKDIR /PasswordstateSimulator

COPY --from=build /src/out .

CMD ["dotnet", "./PasswordstateSimulator.dll"]
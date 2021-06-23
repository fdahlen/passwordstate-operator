FROM mcr.microsoft.com/dotnet/sdk:5.0 as build

RUN mkdir /src
WORKDIR /src
COPY . /src

RUN dotnet publish --configuration Release -o out PasswordstateOperator/PasswordstateOperator.csproj

FROM mcr.microsoft.com/dotnet/runtime:5.0 as runtime

WORKDIR /PasswordstateOperator

COPY --from=build /src/out .

CMD ["dotnet", "./PasswordstateOperator.dll"]
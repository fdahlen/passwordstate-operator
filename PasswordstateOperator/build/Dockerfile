FROM mcr.microsoft.com/dotnet/sdk:5.0 as build

RUN mkdir /src
WORKDIR /src
COPY . /src

RUN cd PasswordstateOperator && dotnet publish --configuration Release -o out

FROM mcr.microsoft.com/dotnet/runtime:5.0 as runtime

WORKDIR /PasswordstateOperator

COPY --from=build /src/out .

CMD ["dotnet", "./PasswordstateOperator.dll"]
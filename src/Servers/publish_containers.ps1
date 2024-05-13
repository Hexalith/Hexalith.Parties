az login --tenant christofle.com
az acr login -n christofle
$version=(minver -t v -v e -d preview)
cd ./Hexalith.Server.Parties
dotnet restore --no-cache
dotnet build --no-restore --disable-build-servers --no-incremental -c Release 
dotnet publish --no-build /t:PublishContainer -c Release -p ContainerRegistry="christofle.azurecr.io" -p ContainerImageTag=$version
pause

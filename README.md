# NuGet Deploy

This is basic demonstration of creating, building, and deploying a .NET Standard NuGet package via GitHub Actions.

# Walkthrough

## Creating the Project

The sample project can be created either via the Visual Studio GUI or using the
`dotnet` command line utility.  Here are the steps for creating this library through
the CLI:

```bash
# Create the class library and unit test projects
dotnet new classlib --name SamplePackage --output src/SamplePackage
dotnet new xunit --name SamplePackage.Tests --output tests/SamplePackage.Tests

# Link the unit test project to the class library
dotnet add tests/SamplePackage.Tests reference src/SamplePackage

# Create the solution file and link the projects
dotnet new sln --name SamplePackage.sln
dotnet sln add src/SamplePackage
dotnet sln add tests/SamplePackage.Tests
```

## Packaging

We need to configure the `SamplePackage` project to be built and packaged in a NuGet
`nupkg` file.  This can be done in a number of ways, but the simpliest is to add XML
attributes to the `csproj` file with information for the NuGet package.

```xml
<PropertyGroup>
  <Id>SamplePackage</Id>
  <Version>1.0.0</Version>
  <Description>Sample package demonstrating deploying via GitHub Actions.</Description>
  <Authors>boflynn</Authors>
  <RepositoryUrl>http://github.com/boflynn/nuget-deploy.git</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
</PropertyGroup>
```

These settings, along with others, are documented here for
[NuGet metadata properties](https://docs.microsoft.com/en-us/dotnet/core/tools/csproj#nuget-metadata-properties).

# Working with GitHub Actions

For an efficient CI/CD pipeline, we want to do the following:

 * Build and run unit tests on every pull request
 * Deploy the NuGet package to our repository on a merged pull request

With [Github Actions](https://github.com/features/actions), we can do all of this without
requiring an external system.  Adding actions to our repository is as simple as creating
new YAML files under the `.github/workflows` folder.  We will be making two, one for each
of the above goals.


## Build and Run Unit Tests on Pull Request

The following file is created in `.github/workflows/build-and-test.yml`:

```yaml
name: Build and Run Unit Tests on PR

on:
  pull_request:
    branches:
      - master

jobs:
  build:
    runs-on: ubuntu-latest
    name: Build and run unit tests on PR
    steps:
    - uses: actions/checkout@722adc63f1aa60a57ec37892e133b1d319cae598 # 2.0.0
    - name: Setup .NET Core
      uses: actions/setup-dotnet@b7821147f564527086410e8edd122d89b4b9602f # 1.4.0
      with:
        dotnet-version: 3.1.100
    - name: Setup NuGet CLI
      uses: NuGet/setup-nuget@255f46e14d51fbc603743e2aa2907954463fbeb9 # 1.0.2
    - name: Restore with nuget
      run: nuget restore
    - name: Build with dotnet
      run: dotnet build --configuration Release
    - name: Test with dotnet
      run: dotnet test --configuration Release --no-build --no-restore
```

The above is self-documenting, but for clarity the steps it performs are:

1. Checks out the code from GitHub for processing
1. Installs .NET Core
1. Installs NuGet
1. Runs `nuget restore` to retrieve referenced packaged
1. Runs `dotnet build` to verify that the solution builds
1. Runs `dotnet test` to verify that all unit tests pass

## Push NuGet Package on Merge

The following file is created in `.github/workflows/publish-nuget-on-merge.yml`:

```yaml
name: Deploy NuGet on Merge

on:
  push:
    branches:
      - master

jobs:
  build:
    runs-on: ubuntu-latest
    name: Deploy NuGet on merge
    steps:
    - uses: actions/checkout@722adc63f1aa60a57ec37892e133b1d319cae598 # 2.0.0
    - name: Setup .NET Core
      uses: actions/setup-dotnet@b7821147f564527086410e8edd122d89b4b9602f # 1.4.0
      with:
        dotnet-version: 3.1.100
    - name: Setup NuGet CLI
      uses: NuGet/setup-nuget@255f46e14d51fbc603743e2aa2907954463fbeb9 # 1.0.2
    - name: Restore with nuget
      run: nuget restore
    - name: Build with dotnet
      run: dotnet build --configuration Release
    - name: Build NuGet package
      run: >-
        SHA=`git rev-parse HEAD`
        BRANCH=`git rev-parse --abbrev-ref HEAD`

        dotnet pack
        --no-build
        --no-restore
        --configuration Release
        -p:RepositoryBranch=$BRANCH
        -p:RepositoryCommit=$SHA
    - name: Push NuGet package
      env:
        NUGET_USERNAME: ${{ secrets.NUGET_USERNAME }}
        NUGET_APIKEY: ${{ secrets.NUGET_APIKEY }}
        NUGET_URL: ${{ secrets.NUGET_URL }}
      run: >-
        dotnet nuget push
        src/SamplePackage/bin/Release/*.nupkg
        --api-key $NUGET_USERNAME:$NUGET_APIKEY
        --source $NUGET_URL
```

This is very similar to the build and test action, with a few additional steps:

 * **Build NuGet package** - packages the library into a NuGet package, passing in
 additional properties about the git status for build information.
 * **Push NuGet package** - publishes the NuGet package to our repository, using the
 `dotnet nuget push` comomand.

In order to publish to our external repository, we need a URL and credentials for the
repository.  Since we never want to commit passwords or other secrets to source
control, we can use the GitHub Secrets feature to store these values and inject
them at runtime.

## GitHub Secrets

Under the Settings tab, click on the Secrets link. You will need to add the following secrets with the appropriate values for your account:

 * `NUGET_USERNAME` - the NuGet repository username for the account that will publish the package
 * `NUGET_APIKEY` - the Nuget repository API key associated with the above username
 * `NUGET_URL` - while not technically a secret, using GitHub Secrets for this lets us keep this configuration out of source control; contains the root URL for the NuGet repository

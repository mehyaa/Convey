# Convey - GitHub Copilot Instructions

## Repository Overview

**Convey** is a .NET microservices framework that provides a simple recipe for building .NET Core microservices. This is an unofficial fork maintained by mehyaa of the original Convey package (snatch-dev/Convey).

### Key Information
- **Language**: C# (.NET 9.0)
- **Project Type**: Multi-project solution producing NuGet packages
- **Size**: 32 library projects + 3 sample applications (35 total projects)
- **Target Framework**: net9.0 (configured in Directory.Build.props)
- **Repository Structure**: Monorepo with multiple NuGet package projects

### Purpose
Convey provides features for quickly setting up microservices including:
- JWT Authentication, CQRS abstractions, Service discovery (Consul)
- Message brokers (RabbitMQ), Logging (Serilog), Metrics (AppMetrics, Prometheus)
- Persistence (MongoDB, Redis, OpenStack OCS), Tracing (Jaeger)
- Security, Vault integration, Swagger documentation, and more

## Build & Development Setup

### Prerequisites
- **.NET SDK**: Version 9.0 or higher (tested with 10.0.100)
- **Git**: For version control operations
- **OS**: Linux, macOS, or Windows (CI runs on ubuntu-latest)

### Essential Commands

#### 1. Restore Dependencies
**ALWAYS run restore before building after a fresh clone or when dependencies change.**
```bash
dotnet restore Convey.sln
```
- **Expected Time**: 10-15 seconds
- **Expected Result**: All 35 projects restored successfully with no errors
- **Note**: This is implicit in `dotnet build` unless you use `--no-restore`

#### 2. Build the Solution
```bash
dotnet build Convey.sln -c Release
```
- **Expected Time**: 12-20 seconds (clean build)
- **Expected Result**: Build succeeded with 0 errors
- **Known Warning**: 1 warning about prerelease dependency (OpenTelemetry.Resources.Container) in Convey.Tracing.Jaeger - THIS IS EXPECTED
- **Note**: Building in Release mode automatically generates NuGet packages (.nupkg files) for each library project

#### 3. Clean Build Artifacts
```bash
dotnet clean Convey.sln -c Release
```
- **Expected Time**: 2-3 seconds
- **Use Case**: Run this before a clean build or when experiencing build issues

#### 4. Format Code
**ALWAYS run format before committing code changes.**
```bash
dotnet format Convey.sln --no-restore
```
- **Expected Time**: 5-10 seconds
- **Expected Result**: May show whitespace formatting errors in existing code - this is NORMAL
- **Important**: The repository has existing formatting issues. Focus only on formatting files you modify.

#### 5. Verify No Formatting Changes Needed
```bash
dotnet format Convey.sln --verify-no-changes --no-restore
```
- **Use Case**: Check if code needs formatting without making changes
- **Exit Code**: Returns non-zero if formatting is needed

#### 6. Run Tests
```bash
dotnet test Convey.sln --no-build --no-restore -c Release
```
- **Expected Result**: No test projects exist - command completes immediately with success
- **Note**: This repository does not have unit tests

### Build Workflow Order

**For making code changes, follow this sequence:**

1. **Start Fresh** (if needed):
   ```bash
   dotnet clean Convey.sln -c Release
   ```

2. **Restore Dependencies** (after fresh clone or dependency changes):
   ```bash
   dotnet restore Convey.sln
   ```

3. **Build**:
   ```bash
   dotnet build Convey.sln -c Release --no-restore
   ```
   - Expected time: 12-20 seconds
   - Generates NuGet packages in each project's bin/Release directory

4. **Format Your Changes**:
   ```bash
   dotnet format Convey.sln --no-restore
   ```
   - Run on files you modified only

5. **Rebuild After Formatting**:
   ```bash
   dotnet build Convey.sln -c Release --no-restore
   ```

## Project Structure

### Root Directory Layout
```
/
├── .github/workflows/main.yml    # GitHub Actions CI/CD pipeline
├── .gitignore                     # Standard .NET gitignore
├── .travis.yml                    # Legacy Travis CI config (reference only)
├── Convey.sln                     # Main solution file (746 lines, 35 projects)
├── Directory.Build.props          # Shared MSBuild properties (target framework, versioning)
├── LICENSE                        # MIT License
├── README.md                      # Project documentation
├── scripts/pack.sh                # Publishes NuGet packages (legacy)
├── samples/                       # 3 sample microservices + docker-compose configs
└── src/                          # 32 library projects (one per Convey package)
```

### Source Projects Structure
Each project in `src/` follows this pattern:
```
src/Convey.{Feature}/
  ├── scripts/dotnet-pack.sh      # Individual package publish script
  └── src/Convey.{Feature}/
      ├── Convey.{Feature}.csproj  # Project file
      ├── Extensions.cs             # Common extension methods
      └── [other source files]
```

### Key Projects (32 total)
- **Core**: Convey (base), Convey.WebApi, Convey.WebApi.CQRS
- **CQRS**: Convey.CQRS.Commands, Convey.CQRS.Events, Convey.CQRS.Queries
- **Messaging**: Convey.MessageBrokers, Convey.MessageBrokers.RabbitMQ, Convey.MessageBrokers.CQRS
- **Outbox Pattern**: Convey.MessageBrokers.Outbox, Convey.MessageBrokers.Outbox.Mongo, Convey.MessageBrokers.Outbox.EntityFramework
- **Authentication**: Convey.Auth, Convey.Auth.Distributed
- **Discovery**: Convey.Discovery.Consul, Convey.LoadBalancing.Fabio
- **Persistence**: Convey.Persistence.MongoDB, Convey.Persistence.Redis, Convey.Persistence.OpenStack.OCS
- **Observability**: Convey.Logging, Convey.Logging.CQRS, Convey.Metrics.AppMetrics, Convey.Metrics.Prometheus, Convey.Tracing.Jaeger, Convey.Tracing.Jaeger.RabbitMQ
- **Documentation**: Convey.Docs.Swagger, Convey.WebApi.Swagger
- **HTTP**: Convey.HTTP, Convey.HTTP.RestEase
- **Security**: Convey.Security, Convey.WebApi.Security, Convey.Secrets.Vault

### Sample Applications
Located in `samples/`:
- **Conveyor.Services.Orders**: Sample orders microservice
- **Conveyor.Services.Deliveries**: Sample deliveries microservice
- **Conveyor.Services.Pricing**: Sample pricing microservice
- **compose/**: Docker compose files for Linux, macOS, and Windows

## Continuous Integration

### GitHub Actions Workflow (.github/workflows/main.yml)
- **Trigger**: Push to `edge` branch, manual dispatch, or repository dispatch
- **Runtime**: ubuntu-latest with .NET 9
- **Steps**:
  1. Setup .NET 9
  2. Checkout source
  3. Build with versioning (format: YY.9.{run_number})
  4. Run tests (no-op since no test projects exist)
  5. Publish NuGet packages to GitHub Packages
  6. Publish symbol packages (.snupkg)

### Build Configuration in CI
```bash
dotnet build Convey.sln \
  --configuration Release \
  --verbosity minimal \
  --property:Version="$VERSION" \
  --property:ContinuousIntegrationBuild=true \
  --property:Deterministic=true \
  --property:DeterministicSourcePaths=true \
  --property:SourceLinkCreate=true \
  --property:DebugType=pdbonly \
  --property:IncludeSymbols=true \
  --property:SymbolPackageFormat=snupkg
```

## Common Issues & Workarounds

### 1. Existing Formatting Issues
**Issue**: Running `dotnet format --verify-no-changes` shows many existing whitespace errors.
**Workaround**: This is expected. Only format the files you modify. The repository has pre-existing formatting inconsistencies.

### 2. NuGet Package Generation Warning
**Issue**: Warning NU5104 about prerelease dependency in Convey.Tracing.Jaeger.
**Workaround**: This is a known issue with OpenTelemetry.Resources.Container package. The warning is expected and can be ignored.

### 3. No Tests Available
**Issue**: `dotnet test` returns immediately with no tests found.
**Explanation**: This is intentional - the repository does not contain unit test projects. Do not add test projects unless explicitly requested.

### 4. Build Artifacts in Git
**Issue**: Ensure bin/, obj/, *.nupkg files are not committed.
**Solution**: The .gitignore is properly configured. Review git status before committing.

## Making Code Changes

### Best Practices
1. **Read the Existing Code Style**: Each project follows similar patterns (Extensions.cs for DI registration, etc.)
2. **Maintain Package Structure**: Each Convey package is self-contained in its own directory
3. **Update All Related Files**: When modifying a feature, check for related files in CQRS, logging, or other packages
4. **Follow Dependency Injection Patterns**: All packages use extension methods like `AddConvey{Feature}()`
5. **Consider Breaking Changes**: These are public NuGet packages - be cautious with API changes

### File Naming Conventions
- **Extensions.cs**: Contains service registration extension methods (IServiceCollection, IApplicationBuilder, IConveyBuilder)
- **{Feature}Options.cs**: Configuration classes in Types/ subdirectories
- **I{Interface}.cs**: Interface definitions
- **Internal/ subdirectory**: Internal implementation classes

### Code Notes
- **TODO in Convey.Persistence.OpenStack.OCS**: HttpRequestBuilder.cs line 34 - regex for multiple '/' cleanup
- **TODO in Convey.Metrics.AppMetrics**: Extensions.cs - workaround for AppMetrics issue #396

## Configuration Files

### Directory.Build.props
- Defines shared properties for all projects
- **Target Framework**: net9.0
- **Authors**: mehyaa, DevMentors.io
- **License**: MIT
- **Repository URL**: https://github.com/mehyaa/Convey.git
- **Local Package Generation**: Set GenerateLocalPackages=true to enable local NuGet package generation

### .gitignore
Standard Visual Studio / .NET gitignore including:
- Build artifacts (bin/, obj/, *.dll, *.pdb, *.nupkg)
- IDE files (.vs/, .idea/, *.user)
- Logs and temporary files

## Architecture Notes

### Dependency Graph
- **Convey** (base) is the foundation - all other packages depend on it
- **Convey.WebApi** depends on Convey
- **Convey.WebApi.CQRS** depends on Convey.WebApi and CQRS packages
- **Convey.MessageBrokers.CQRS** bridges messaging and CQRS
- Outbox packages depend on Convey.MessageBrokers.Outbox base package

### Extension Pattern
All packages follow the builder pattern:
```csharp
services.AddConvey()
    .AddWebApi()
    .AddRabbitMq()
    .AddMongo()
    .AddRedis();
```

## Additional Notes

- **No Unit Tests**: This repository does not have test projects. Changes should be validated by building successfully and reviewing the code.
- **Package Versioning**: Handled by CI/CD based on build number
- **Samples**: Use the sample applications in `samples/` to understand how packages work together
- **Docker Support**: Docker compose files are provided for running sample services with dependencies

---

## Quick Command Reference

```bash
# Fresh start
dotnet clean Convey.sln -c Release
dotnet restore Convey.sln
dotnet build Convey.sln -c Release --no-restore

# Format code (files you changed)
dotnet format Convey.sln --no-restore

# Check formatting
dotnet format Convey.sln --verify-no-changes --no-restore

# Rebuild
dotnet build Convey.sln -c Release --no-restore
```

**Trust these instructions** - they are validated against the actual repository. Only search for additional information if you encounter unexpected behavior or errors not documented here.

# SonarQube Code Quality & Coverage Integration

This document explains the SonarQube integration for code quality analysis and coverage reporting in the CI/CD pipeline.

## Overview

The CI/CD pipeline automatically:
1. Generates code coverage reports during test execution
2. Analyzes code quality with SonarQube
3. Uploads coverage data to SonarQube for analysis
4. Provides quality gates for pull requests

## Configuration

### GitHub Secrets

The following secrets must be configured in your GitHub repository:

- `SONAR_TOKEN`: Authentication token from SonarQube/SonarCloud
- `SONAR_HOST_URL`: SonarQube server URL (e.g., https://sonarcloud.io)

### SonarQube Project Settings

- **Project Key**: `anubhavsharmawork_fintech`
- **Organization**: `anubhavsharmawork`

### Configuration Method

**Important**: SonarScanner for .NET does **not** use `sonar-project.properties` files. All configuration is passed via command-line parameters in the workflow file using `/d:` flags.

### Coverage Configuration

#### Test Project Setup

The test project (`Tests/Tests.csproj`) includes:
- `coverlet.collector` package for coverage collection
- OpenCover format for SonarQube compatibility

#### Coverage Exclusions

The following are excluded from coverage analysis:
- Database migrations (`**/Migrations/**`)
- Program entry points (`**/Program.cs`)
- Startup configuration (`**/Startup.cs`)
- Test projects themselves (`**/Tests/**`)

#### General Exclusions

The following are excluded from general analysis:
- Build artifacts (`**/bin/**`, `**/obj/**`)
- Static web assets (`**/wwwroot/**`)
- Node modules (`**/node_modules/**`)
- Client-side files (`**/*.js`, `**/*.css`, `**/*.html`)

## CI/CD Workflow

### Build and Test Job

The `build-test` job performs:

1. **Setup**
   - Checkout code with full git history
   - Setup .NET 10 SDK
   - Setup Java 17 (required for SonarQube scanner)
   - Cache SonarQube packages and scanner

2. **Analysis Begin**
   - Initialize SonarQube scanner
   - Configure coverage report paths
   - Set coverage exclusions

3. **Build**
   - Restore NuGet packages
   - Build solution in Release mode

4. **Test with Coverage**
   - Run all tests
   - Collect code coverage in OpenCover format
   - Generate coverage reports for each test project

5. **Analysis End**
   - Upload coverage data to SonarQube
   - Complete code quality analysis
   - Report results

6. **Artifact Upload**
   - Upload coverage reports as GitHub artifacts
   - Retain for 30 days

## Local Development

### Generate Coverage Reports Locally

```bash
# Run tests with coverage
dotnet test Fintech.sln \
  --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

# Coverage files will be generated in:
# Tests/TestResults/{guid}/coverage.opencover.xml
```

### View Coverage Reports

#### Using ReportGenerator

```bash
# Install ReportGenerator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator \
  -reports:"**/coverage.opencover.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:Html

# Open the report
# Windows: start coveragereport/index.html
# macOS: open coveragereport/index.html
# Linux: xdg-open coveragereport/index.html
```

### Run SonarQube Analysis Locally

```bash
# Install SonarQube scanner
dotnet tool install --global dotnet-sonarscanner

# Begin analysis (all configuration via command line)
dotnet sonarscanner begin \
  /k:"anubhavsharmawork_fintech" \
  /o:"anubhavsharmawork" \
  /d:sonar.token="YOUR_SONAR_TOKEN" \
  /d:sonar.host.url="https://sonarcloud.io" \
  /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml" \
  /d:sonar.coverage.exclusions="**/Migrations/**,**/Program.cs,**/Startup.cs,**/Tests/**" \
  /d:sonar.exclusions="**/bin/**,**/obj/**,**/Migrations/**,**/wwwroot/**,**/node_modules/**,**/*.js,**/*.css,**/*.html"

# Build
dotnet build Fintech.sln --configuration Release

# Test with coverage
dotnet test Fintech.sln \
  --configuration Release \
  --no-build \
  --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

# End analysis
dotnet sonarscanner end /d:sonar.token="YOUR_SONAR_TOKEN"
```

**Note**: For .NET projects, do **not** create a `sonar-project.properties` file. The SonarScanner for .NET ignores it and all configuration must be passed as command-line arguments.

## SonarQube Dashboard

### Accessing Results

1. Visit your SonarQube/SonarCloud project:
   - **URL**: https://sonarcloud.io/project/overview?id=anubhavsharmawork_fintech

2. View key metrics:
   - Code coverage percentage
   - Code smells
   - Bugs
   - Security vulnerabilities
   - Technical debt
   - Duplicated code

### Quality Gates

Default quality gates check:
- **Coverage**: > 80% (configurable)
- **Duplicated Lines**: < 3%
- **Maintainability Rating**: A
- **Reliability Rating**: A
- **Security Rating**: A

Pull requests will show quality gate status and prevent merging if gates fail.

## Interpreting Results

### Coverage Metrics

- **Line Coverage**: Percentage of executable lines covered by tests
- **Branch Coverage**: Percentage of conditional branches covered by tests
- **Method Coverage**: Percentage of methods covered by tests

### Code Quality Issues

- **Blocker**: Must be fixed immediately
- **Critical**: Should be fixed as soon as possible
- **Major**: Should be fixed
- **Minor**: Should be reviewed
- **Info**: For information only

## Troubleshooting

### Coverage Not Showing in SonarQube

1. Verify coverage files are generated:
   ```bash
   find . -name "coverage.opencover.xml"
   ```

2. Check SonarQube scanner logs for coverage import errors

3. Ensure coverage report paths match in the workflow's `dot

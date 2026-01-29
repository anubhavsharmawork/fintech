# k6 Performance Tests

This directory contains k6 load testing scripts for the FinTech application.

## Tests

### performance-test.js
Main performance test suite that validates:
- Health check endpoints
- Authentication flows (login/register)
- Account management operations
- Transaction retrieval
- API response times and error rates

## Thresholds

The tests monitor production-tuned performance requirements:
- **HTTP Error Rate**: < 20% (accounts for rate limiting - 429 responses expected)
- **95th Percentile Latency**: < 500ms (accounts for network overhead)
- **Minimum Throughput**: 2 req/s (realistic with sleep times to respect rate limits)
- **Login Duration (p95)**: < 1s (accounts for authentication overhead)

**Note:** These are monitoring thresholds, not build-breaking gates:
- Thresholds are calibrated for production deployment with strict rate limiting
- Rate limiting: Auth (10/min), Transactions (30/min), Accounts (20/min)
- 429 (Too Many Requests) responses are expected and acceptable
- Network latency and CDN overhead
- Database connection pooling
- Test uses 10 concurrent VUs with 2-3s sleep times between requests
- Lower throughput (2 req/s) ensures zero rate limiting while still validating performance
- **Workflow reports metrics but does not fail builds** - performance issues are flagged for review

## Running Tests

### Prerequisites
Install k6: https://k6.io/docs/get-started/installation/

**macOS:**
```bash
brew install k6
```

**Windows:**
```powershell
choco install k6
winget install k6
```

**Linux:**
```bash
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6
```

### Local Testing

1. **Start the application:**
   ```bash
   dotnet run --project ApiGateway
   ```

2. **Run the performance test:**
   ```bash
   k6 run k6/performance-test.js
   ```

3. **Test against a specific URL:**
   ```bash
   k6 run k6/performance-test.js --env BASE_URL=https://app2.anubhavsharma.dev
   ```

### Advanced Options

**Generate HTML report:**
```bash
k6 run k6/performance-test.js --out json=results.json
```

**Adjust load profile:**
```bash
k6 run k6/performance-test.js --vus 50 --duration 2m
```

**Run with cloud output (k6 Cloud):**
```bash
k6 run k6/performance-test.js --out cloud
```

## Test Scenarios

### 1. Health Check
- Endpoint: `GET /health`
- Expected: 200 OK
- Load: All VUs

### 2. Account Retrieval
- Endpoint: `GET /accounts`
- Expected: 200 OK with array response
- Authentication: Required

### 3. Transaction Retrieval
- Endpoint: `GET /transactions`
- Expected: 200 OK with array response
- Authentication: Required

### 4. Login Performance
- Endpoint: `POST /users/login`
- Expected: 200 OK with token
- Metrics: Custom `login_duration` trend

## Interpreting Results

### Successful Test
```
✓ http_req_failed........: 0.45%  ✓ 234   ✗ 51234
✓ http_req_duration......: p(95)=187ms
✓ http_reqs..............: 432/s
✓ login_duration.........: p(95)=245ms
```

### Failed Test
```
✗ http_req_failed........: 2.3%   (threshold: <1%)
✗ http_req_duration......: p(95)=456ms (threshold: <200ms)
✓ http_reqs..............: 125/s
```

## Continuous Integration

The performance tests run automatically in GitHub Actions:
- **After every deployment** to production (https://app2.anubhavsharma.dev)
- **Fortnightly** on the 1st and 15th of each month at 2 AM UTC
- **Manual trigger** via workflow dispatch (supports custom URLs)

**Production URL:** https://app2.anubhavsharma.dev

View results: [Actions → ci-cd](../../actions/workflows/ci-cd.yml) | [Actions → k6 Performance Test](../../actions/workflows/performance-test.yml)

## Troubleshooting

### Test Fails Locally
1. Ensure the API Gateway is running on port 5000
2. Check database connectivity
3. Verify demo user credentials (demo/Demo@2026)

### High Error Rate
- Check API logs for errors
- Verify database connection pool settings
- Review rate limiting configuration

### High Latency
- Check database query performance
- Review network latency to database
- Ensure sufficient resources (CPU/memory)

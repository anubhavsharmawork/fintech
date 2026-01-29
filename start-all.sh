#!/usr/bin/env bash
set -euo pipefail

PORT=${PORT:-5000}
USER_PORT=${USER_PORT:-5001}
ACCOUNT_PORT=${ACCOUNT_PORT:-5002}
TRANSACTION_PORT=${TRANSACTION_PORT:-5003}

export USER_PORT
export ACCOUNT_PORT
export TRANSACTION_PORT

echo "Starting UserService on port ${USER_PORT}"
dotnet /app/UserService/UserService.dll --urls http://0.0.0.0:${USER_PORT} &

echo "Starting AccountService on port ${ACCOUNT_PORT}"
dotnet /app/AccountService/AccountService.dll --urls http://0.0.0.0:${ACCOUNT_PORT} &

echo "Starting TransactionService on port ${TRANSACTION_PORT}"
dotnet /app/TransactionService/TransactionService.dll --urls http://0.0.0.0:${TRANSACTION_PORT} &

# NotificationService is a worker; run in background without binding to HTTP
if [ -f /app/NotificationService/NotificationService.dll ]; then
  echo "Starting NotificationService (worker)"
  dotnet /app/NotificationService/NotificationService.dll &
fi

# Wait for backend services to initialize before starting gateway
echo "Waiting for backend services to start..."
sleep 3

echo "Starting ApiGateway on port ${PORT}"
cd /app/ApiGateway
exec dotnet ApiGateway.dll --urls http://0.0.0.0:${PORT}

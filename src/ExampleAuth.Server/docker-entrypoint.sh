#!/bin/sh
set -e

if [ -n "$EXAMPLEAUTH_SIGNING_CERT_BASE64" ]; then
  mkdir -p /app/certs
  echo "$EXAMPLEAUTH_SIGNING_CERT_BASE64" | base64 -d > /app/certs/exampleauth.pfx
  export EXAMPLEAUTH_SIGNING_CERT_PATH=/app/certs/exampleauth.pfx
fi

exec dotnet ExampleAuth.Server.dll

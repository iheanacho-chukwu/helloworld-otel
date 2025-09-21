
```bash
cd hello-otel/out/
chmod +x HelloOtel
export ASPNETCORE_URLS="http://0.0.0.0:8080"
```
# Pick ONE:

# A) gRPC via 4317 (h2c)
```bash
export OTEL_SERVICE_NAME=hello-otel
export OTEL_ENV=staging
export OTEL_EXPORTER_OTLP_PROTOCOL=grpc
export OTEL_EXPORTER_OTLP_ENDPOINT=http://$URL:4317
export DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2UNENCRYPTEDSUPPORT=1
./HelloOtel   # or: dotnet HelloOtel.dll
```

# B) HTTP via 4318
```bash
export OTEL_SERVICE_NAME=hello-otel
export OTEL_ENV=staging
export OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
export OTEL_EXPORTER_OTLP_TRACES_ENDPOINT=http://$URL:4318/v1/traces
export OTEL_EXPORTER_OTLP_LOGS_ENDPOINT=http://$URL:4318/v1/logs
export OTEL_EXPORTER_OTLP_METRICS_ENDPOINT=http://$URL:4318/v1/metrics
./HelloOtel   # or: dotnet HelloOtel.dll
```

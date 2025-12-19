#!/bin/bash

set -e

echo "╔════════════════════════════════════════════════════════════════╗"
echo "║     MinimalWorker OpenTelemetry Stack - Quick Start Script    ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo

# Function to check if Docker is running
check_docker() {
    if ! docker info > /dev/null 2>&1; then
        echo "❌ Error: Docker is not running. Please start Docker and try again."
        exit 1
    fi
}

# Function to check if ports are available
check_ports() {
    local ports=(3000 4317 9090 16686)
    for port in "${ports[@]}"; do
        if lsof -Pi :$port -sTCP:LISTEN -t >/dev/null 2>&1 ; then
            echo "⚠️  Warning: Port $port is already in use. Docker services may fail to start."
        fi
    done
}

# Main execution
echo "🔍 Checking prerequisites..."
check_docker
echo "✅ Docker is running"

check_ports

echo
echo "🚀 Starting observability stack..."
docker-compose up -d

echo
echo "⏳ Waiting for services to be ready..."
sleep 5

# Check if services are up
echo
echo "📊 Checking service health..."
docker-compose ps

echo
echo "✅ Observability stack is ready!"
echo
echo "╔════════════════════════════════════════════════════════════════╗"
echo "║                     Access Your Dashboards                     ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo
echo "  🔍 Jaeger (Traces):     http://localhost:16686"
echo "  📊 Prometheus (Metrics): http://localhost:9090"
echo "  📈 Grafana (Dashboards): http://localhost:3000 (admin/admin)"
echo "  📝 Loki (Logs):         http://localhost:3100"
echo
echo "╔════════════════════════════════════════════════════════════════╗"
echo "║                    Run the Sample Application                  ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo
echo "  In a new terminal, run:"
echo "  $ cd samples/MinimalWorker.OpenTelemetry.Sample"
echo "  $ dotnet run"
echo
echo "╔════════════════════════════════════════════════════════════════╗"
echo "║                        Useful Commands                         ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo
echo "  View logs:           docker-compose logs -f"
echo "  Stop stack:          docker-compose down"
echo "  Stop & remove data:  docker-compose down -v"
echo "  Restart stack:       docker-compose restart"
echo
echo "📚 For more information, see README.md"
echo

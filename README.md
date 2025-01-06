# Oh My Linkki

An AI-powered assistant for tracking Linkki bus lines in Jyväskylä, Finland. The application provides real-time bus locations and an AI chat interface for public transportation queries.

## Features

- Real-time bus tracking using GTFS Realtime data
- Interactive map showing bus locations
- AI chat assistant for transportation queries
- WebSocket-based live updates

## Project Structure

- `src/api/` - .NET 9 backend service
- `src/ui/` - React + TypeScript frontend

## Development Setup

### Prerequisites

- Visual Studio Code
- Dev Containers extension for VS Code
- Docker Desktop

### Getting Started

1. Clone the repository and open it in VS Code
2. When prompted, click "Reopen in Container" or use Command Palette (F1) and select "Dev Containers: Reopen in Container"
3. VS Code will build and start the development container with all required dependencies

### Configuration

Create `src/api/appsettings.Development.json` with the following structure:

```json
{
  "LinkkiImport": {
    "WalttiUsername": "your_username",
    "WalttiPassword": "your_password",
    "ImportInterval": 3000
  },
  "OpenAi": {
    "Endpoint": "your_openai_endpoint",
    "ApiKey": "your_openai_key",
    "DeploymentName": "your_deployment_name"
  },
  "CosmosDb": {
    "ConnectionString": "your_cosmos_connection_string"
  },
  "WebPubSubEndpoint": "your_pubsub_endpoint"
}
```

### Running the Application

1. Start the backend:

```sh
cd src/api
dotnet run
``sh
2. Start the frontend:
```sh
cd src/ui
bun run dev --host
```

The application will be available at:

- Frontend: <http://localhost:5173>
- Backend: <http://localhost:5074>


## License

This project is licensed under the MIT License.

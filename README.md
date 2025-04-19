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
    "Endpoint": "your_openai_endpoint"
  },
  "CosmosDb": {
    "ConnectionString": "your_cosmos_connection_string"
  },
  "WebPubSub":{
    "Endpoint": "your_pubsub_endpoint"
  }
}
```

Create `src/ui/.env` with the following structure to overwrite API_URL.

```properties

# Development in devcontainer (default)
API_URL="http://localhost:5074"

# Development with IDE on host
# API_URL="http://host.docker.internal:5074"
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

## Deploy to the Azure

Follow these steps to deploy the solution to Azure.

### Build images

Use following commands to create and push images to docker hub.

Set username and versions.

```sh
export DOCKER_HUB_USERNAME=<your_username>

export OHMYLINKKI_API_VERSION=<version>
export OHMYLINKKI_UI_VERSION=<version>
export OHMYLINKKI_MCP_SERVER_VERSION=<version>
export OHMYLINKKI_NGINX_PROXY_VERSION=<version>
```

Tag and push images to Docker Hub.

```sh
docker build -t $DOCKER_HUB_USERNAME/ohmylinkki-api:$OHMYLINKKI_API_VERSION -f src/api/Dockerfile .
docker push $DOCKER_HUB_USERNAME/ohmylinkki-api:$OHMYLINKKI_API_VERSION

docker build -t $DOCKER_HUB_USERNAME/ohmylinkki-ui:$OHMYLINKKI_UI_VERSION -f src/ui/Dockerfile src/ui/
docker push $DOCKER_HUB_USERNAME/ohmylinkki-ui:$OHMYLINKKI_UI_VERSION

docker build -t $DOCKER_HUB_USERNAME/ohmylinkki-nginx-proxy:$OHMYLINKKI_NGINX_PROXY_VERSION -f nginx-proxy/Dockerfile nginx-proxy
docker push $DOCKER_HUB_USERNAME/ohmylinkki-nginx-proxy:$OHMYLINKKI_NGINX_PROXY_VERSION

docker build -t $DOCKER_HUB_USERNAME/ohmylinkki-mcp-server:$OHMYLINKKI_MCP_SERVER_VERSION -f src/McpServer/Dockerfile .
docker push $DOCKER_HUB_USERNAME/ohmylinkki-mcp-server:$OHMYLINKKI_MCP_SERVER_VERSION


```

### Deploy Azure Infra

Follow these steps to deploy solutions to Azure:

1. Open terminal
2. Log in using your Microsoft Entra ID credentials: `az login --use-device-code`
3. Go to the directory: `cd infra/bicep/stacks/ohmylinkki`
4. First verify the changes using what-if command:

    ```shell
    az deployment sub what-if --subscription {subscription} --location {location} --template-file main.bicep --parameters {env}.bicepparam
    ```

5. Deploy the stack:

    ```shell
    az stack sub create --name ohmylinkki --subscription {subscription} --location {location} --deny-settings-mode none --action-on-unmanage detachAll --template-file main.bicep --parameters {env}.bicepparam
    ```

## License

This project is licensed under the MIT License.

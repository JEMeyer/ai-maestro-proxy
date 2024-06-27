# 🌐 **AI Maestro Proxy: A Comprehensive Guide**

## Overview:
AI Maestro Proxy is a versatile application designed to manage and distribute requests among different AI models. It serves as a proxy server that receives client requests, routes them to the appropriate backend AI model based on availability, and then forwards the response back to the client. This system ensures optimal resource utilization and efficient management of AI resources.

## Usage:
AI Maestro Proxy can be used in various AI applications where multiple models are available for processing different tasks. By pointing your AI calls to this server, you can easily manage which model handles each request based on its availability. The system is designed to handle two primary types of requests: diffusion and Ollama.

### Local Development

For environment variable management, the project uses an `.env` file that contains connection strings for MariaDB and Redis. When running the application locally with `dotnet run`, you will need to provide real values for these variables. To streamline this process, there are two scripts available - `run.sh` for Linux/MacOS systems and `run.ps1` for Windows users. These scripts take the variables from your `.env` file and add them to the computer's environment variables before executing `dotnet run`. This approach ensures that the necessary configuration settings are in place for running the application locally as well as having it use the same method for docker environment variable setting. The port for local development will be `5000`, not the `8080` for production builds.

### Docker Run

Ports are configured with standard docker flags. `.env` file is used to pass connection strings for MariaDB and Redis (ConnectionStrings__MariaDb and ConnectionStrings__Redis) - see .env.template for more info.

To use the most recent image, pull the `latest` tag:

```bash
docker run --env-file=.env -p 8080:8080 ghcr.io/jemeyer/ai-maestro-proxy:latest
```

This will start the server and make it accessible at <http://localhost:8080>.

### Docker Compose

You can also use the proxy server with Docker Compose. Here's an example docker-compose.yml file:

```yaml
services:
  ai-maestro-proxy:
    image: ghcr.io/jemeyer/ai-maestro-proxy:latest
    ports:
      - "8080:8080"
    env_file:
      - .env
```

This configuration will start a container using the latest image and make it accessible at <http://localhost:8080>. It will read the .env file for environment variables.

## Handling Diffusion Requests:
Diffusion requests involve image-related tasks such as text-to-image or image-to-image generation. To use AI Maestro Proxy for diffusion requests, clients should send a POST request to the following endpoints on the proxy server:

- Text-to-Image Generation: `/txt2img`
- Image-to-Image Generation: `/img2img`

The request body should align with any requirements from [JEMeyer's stablediffusion-fastapi-multigpu repo](https://github.com/JEMeyer/stablediffusion-fastapi-multigpu), as the proxy server forwards these requests to the appropriate backend model. Please note that there is a Docker file available for this service (you can pull it using `docker pull ghcr.io/jemeyer/stablediffusion-fastapi-multigpu:latest`).

## Handling Ollama Requests:
Ollama requests are used for various natural language processing tasks, such as chat generation or text embedding. To use AI Maestro Proxy for Ollama requests, clients should send a POST request to the following endpoints on the proxy server:

- Chat Generation: `/api/chat`
- Text Generation: `/api/generate`
- Text Embeddings: `/api/embeddings`

The request body should align with any requirements from [Ollama's official repository](https://github.com/ollama/ollama), as the proxy server forwards these requests to the appropriate backend model. Please note that there is a Docker image available for this service (you can pull it using `docker pull ollama/ollama`).

## How It Works:
1. Clients send AI requests to the AI Maestro Proxy server, specifying the desired model and other parameters in the request body.
2. The HandlerService extracts the RequestModel from the HTTP context of the incoming request.
3. If it's an Ollama request, the system sets appropriate defaults for streaming and keep-alive settings in the RequestModel -  `stream` behaves as it normally would for Ollama, which is to default to true if unset. `keep_alive` is hardcoded to -1 (infinite) to keep the model in VRAM.
4. The HandlerService then calls the GpuManagerService to get an available GPU assignment based on the requested model.
5. Once a suitable GPU assignment is obtained, ProxiedRequestService routes the request to the appropriate AI model's server.
6. After processing the request and generating a response, the assigned GPU resources are unlocked in the GpuManagerService.
7. Finally, the system forwards the response from the AI model's server back to the original client through HandlerService.

## Contributing

Contributions are welcome! If you find any issues or have suggestions for improvements, please open an issue or submit a pull request.

## License

This project is licensed under Apache 2.0 - see the [LICENSE](LICENSE) file for details.

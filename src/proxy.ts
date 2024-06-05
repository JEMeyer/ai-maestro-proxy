import express from 'express';
import {
  createProxyMiddleware,
  Options,
  responseInterceptor,
} from 'http-proxy-middleware';
import { Compute } from './utilities/computeStatus';
import { IncomingMessage } from 'http';
import { reserveGPU } from './utilities/configuration';
import { initializeRedis } from './services/db';
import { GpuStatusMutex } from './utilities/mutex';

const app = express();

// Middleware to parse JSON bodies
app.use(express.json());

const modelRouter = async (req: IncomingMessage) => {
  const expressReq = req as express.Request & {
    gpuIds: string[];
  };
  const modelName = expressReq.body.model;
  const result = await reserveGPU(modelName);

  if (result instanceof Error) return undefined;

  if (result === undefined) {
    // If no available server is found, wait for a short interval and retry
    setTimeout(() => {
      return modelRouter(req);
    }, 300);
  } else {
    // Attach the GPU IDs to the request object
    expressReq.gpuIds = result.gpuIds;

    return `${result.serverUrl}${expressReq.originalUrl}`;
  }
};

const handleResponse = async (responseBuffer: any, proxyRes: any) => {
  const release = await GpuStatusMutex.acquire();
  try {
    // Retrieve the GPU IDs from the request object
    const gpuIds = proxyRes.req.gpuIds;

    // Now that the request is done, we can free up the GPUs
    await Compute.markAvailable(gpuIds);
    return responseBuffer; // Return the original response buffer
  } finally {
    release();
  }
};

const diffusionProxyOptions: Options = {
  changeOrigin: true,
  router: modelRouter,
  selfHandleResponse: true,
  on: {
    proxyRes: responseInterceptor(handleResponse),
  },
};

const ollamaProxyOptions: Options = {
  changeOrigin: true,
  router: modelRouter,
  selfHandleResponse: true, // Required for responseInterceptor to work
  on: {
    proxyReq: function (proxyReq, req) {
      // We need to modify only the ollama requests to enforce keep_alive
      const expressReq = req as express.Request;
      const bodyData = expressReq.body;
      bodyData['keep_alive'] = -1;
      const bodyString = JSON.stringify(bodyData);
      proxyReq.setHeader('Content-Length', Buffer.byteLength(bodyString));
      proxyReq.write(bodyString);
    },
    proxyRes: responseInterceptor(handleResponse),
  },
};

// Create the proxy middleware
const ollamaProxy = createProxyMiddleware(ollamaProxyOptions);
const diffusionProxy = createProxyMiddleware(diffusionProxyOptions);

// Apply the proxy middleware to the specific endpoints
app.use('/api/completions', ollamaProxy);
app.use('/api/chat', ollamaProxy);
app.use('/api/embeddings', ollamaProxy);

// Note, we'll use the same port for the diffusion to consolidate apps for proxy
app.use('/txt2img', diffusionProxy);
app.use('/img2img', diffusionProxy);

// Start the server
const port = 11434;
app.listen(port, async () => {
  await initializeRedis();
  console.log(`Load balancer server is running on port ${port}`);
});

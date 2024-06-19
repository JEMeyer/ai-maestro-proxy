import dotenv from 'dotenv';
dotenv.config();
import express, { Request, Response, NextFunction } from 'express';
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
import { v4 as uuidv4 } from 'uuid';
import { deleteModelAssignmentCache } from './services/assignments';

const app = express();

// Middleware to parse JSON bodies
app.use(express.json());

// Middleware to add a unique ID to each request
app.use((req: Request, res: Response, next: NextFunction) => {
  const requestId = uuidv4();
  req.headers['x-request-id'] = requestId;
  next();
});

const modelRouter = async (
  req: IncomingMessage
): Promise<string | undefined> => {
  const expressReq = req as express.Request;
  const modelName = expressReq.body.model;
  const requestId = expressReq.headers['x-request-id'] as string;
  const result = await reserveGPU(modelName, requestId);

  if (result instanceof Error) return undefined;

  if (result === undefined) {
    // If no available server is found, wait for a short interval and retry
    setTimeout(() => {
      return modelRouter(req);
    }, 300);
  } else {
    return `${result.serverUrl}${expressReq.originalUrl}`;
  }
};

const handleResponse = async (responseBuffer: any, proxyRes: any) => {
  const release = await GpuStatusMutex.acquire();
  try {
    const symbols = Object.getOwnPropertySymbols(proxyRes.req);
    const kOutHeadersSymbol = symbols.find(
      (sym) => sym.toString() === 'Symbol(kOutHeaders)'
    );

    if (kOutHeadersSymbol != null) {
      const requestId = proxyRes.req[kOutHeadersSymbol]['x-request-id'][1];
      if (requestId) {
        await Compute.markAvailable(requestId);
      } else {
        console.error('Request does not have ID in response handler');
      }
    } else {
      console.error("Symbol 'kOutHeaders' not found on proxyRes.req");
    }

    // Return the original response buffer
    return responseBuffer;
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

// Used to wipe out the Redis cache in case of bad data - will then re-request from the database
app.delete('/cache', async (req, res) => {
  try {
    await deleteModelAssignmentCache();
    res.status(200).send({ message: 'Cache cleared successfully' });
  } catch (error) {
    res.status(500).send({ error: 'Failed to clear cache' });
  }
});

// Start the server
const port = 11434;
app.listen(port, async () => {
  await initializeRedis();
  console.log(`Load balancer server is running on port ${port}`);
});

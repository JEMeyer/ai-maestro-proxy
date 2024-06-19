import dotenv from 'dotenv';
dotenv.config();
import express, { Request, Response, NextFunction } from 'express';
import morgan from 'morgan';
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
import axios from 'axios';

const app = express();

// Middleware to parse JSON bodies
app.use(express.json());

// Logging middleware
app.use(morgan('dev'));

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

  console.log(
    '[PROXY]',
    `Forwarding to ${result?.serverUrl}${expressReq.originalUrl}`
  );

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

// Create the proxy middleware
const diffusionProxy = createProxyMiddleware(diffusionProxyOptions);

// Note, we'll use the same port for the diffusion to consolidate apps for proxy
app.use('/txt2img', diffusionProxy);
app.use('/img2img', diffusionProxy);

// Unfortunately ollama proxy made me wanna minecraft myself so we just call on the users behalf
app.use('/api/generate', handleOllamaRequest);
app.use('/api/chat', handleOllamaRequest);
app.use('/api/embeddings', handleOllamaRequest);

// Used to wipe out the Redis cache in case of bad data - will then re-request from the database
app.delete('/cache', async (req, res) => {
  try {
    await deleteModelAssignmentCache();
    res.status(200).send({ message: 'Cache cleared successfully' });
  } catch (error) {
    res.status(500).send({ error: 'Failed to clear cache' });
  }
});

async function handleOllamaRequest(req: Request, res: Response) {
  try {
    const stream = req.body.stream as boolean;
    const requestId = req.headers['x-request-id'] as string;

    const result = await reserveGPU(req.body.model, requestId);
    if (result instanceof Error) {
      res.status(500).send('Error reserving GPU');
      return;
    }

    if (result == null) return;

    // promise chain the ollama call. Avoid await so we can service other calls
    axios({
      method: req.method,
      url: `${result.serverUrl}${req.originalUrl}`,
      data: { ...req.body, keep_alive: -1 },
      responseType: stream ? 'stream' : 'json',
    })
      .then((response) => {
        if (stream) {
          response.data.on('data', (chunk: Buffer) => {
            res.write(chunk);
          });

          response.data.on('end', () => {
            res.end();
            Compute.markAvailable(requestId);
          });
        } else {
          res.status(response.status).json(response.data);
          Compute.markAvailable(requestId);
        }
      })
      .catch((error) => {
        console.error('Error proxying Ollama request:', error);
        res.status(500).send('Error proxying Ollama request');
      });
  } catch (error) {
    console.error('Error proxying Ollama request:', error);
    res.status(500).send('Error proxying Ollama request');
  }
}

// Start the server
const port = 11434;
app.listen(port, async () => {
  await initializeRedis();
  console.log(`Load balancer server is running on port ${port}`);
});

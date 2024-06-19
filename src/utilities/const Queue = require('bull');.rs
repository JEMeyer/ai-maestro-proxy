const Queue = require('bull');

const gpuQueue = new Queue('gpuQueue', 'redis://localhost:6379');

// Define a job processor for the queue
gpuQueue.process(async (job) => {
  const { modelName, requestId } = job.data;
  const result = await reserveGPU(modelName, requestId);
  if (result) {
    // If a GPU is available, process the job
    console.log(`Processing job ${requestId} on GPU ${result.gpuIds}`);
    // Perform the necessary computations
    // ...
  } else {
    // If no GPU is available, retry the job after a delay
    console.log(`No GPU available for job ${requestId}. Retrying...`);
    await gpuQueue.add(job.data, { delay: 3000 });
  }
});

// When a GPU is marked available, re-check the queue
Compute.on('gpuAvailable', async (gpuId) => {
  const jobs = await gpuQueue.getJobs(['waiting', 'delayed']);
  for (const job of jobs) {
    const { modelName, requestId } = job.data;
    const result = await reserveGPU(modelName, requestId);
    if (result && result.gpuIds.includes(gpuId)) {
      // If the GPU is available, move the job to the active queue
      await gpuQueue.promote(job.id);
    }
  }
});

// Add jobs to the queue
async function handleOllamaRequest(req: Request, res: Response) {
  const modelName = req.body.model;
  const requestId = req.headers['x-request-id'] as string;
  await gpuQueue.add({ modelName, requestId });
  res.status(202).send(`Job ${requestId} added to queue`);
}

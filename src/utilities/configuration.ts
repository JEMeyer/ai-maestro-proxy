import { Compute } from './computeStatus';
import { getModelAssignments } from '../services/assignments';
import { GpuStatusMutex } from './mutex';

/**
 * This function reserves a GPU for a given model by checking the availability of its associated servers.
 * If it finds a server that is not busy, it marks the server as busy and returns the server address.
 * If no free server is found, it returns undefined.
 * @param {string} modelName - The name of the model for which to reserve a GPU.
 * @returns {string | undefined} - The address of the reserved server if available; otherwise, undefined.
 */
export const reserveGPU = async (
  modelName: string
): Promise<{ serverUrl: string; gpuIds: string[] } | undefined> => {
  const release = await GpuStatusMutex.acquire();
  try {
    // Get model gpuIds we can use
    const assignments = await getModelAssignments(modelName);

    // Loop and find the first one that is free, otherwise return undefined
    for (const assignment of assignments) {
      const gpuIds = assignment.gpu_ids.split(',');
      const isBusy = await Compute.isBusy(gpuIds);

      if (!isBusy) {
        Compute.markBusy(gpuIds);
        return {
          serverUrl: `http://${assignment.ip_addr}:${assignment.port}`,
          gpuIds,
        };
      }
    }
    return undefined;
  } finally {
    release();
  }
};

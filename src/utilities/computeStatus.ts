/**
 * The ComputeStatus class manages the busy status of gpus
 */
class ComputeStatus {
  // keys are the gpu ids, values are the request id that is using it.
  private pendingRequests: Map<string, string | undefined> = new Map();

  // True if any gpu in the list is busy
  public async isBusy(gpuIds: string[]): Promise<boolean> {
    return gpuIds.some(
      (gpuId) => this.pendingRequests.get(gpuId) !== undefined
    );
  }

  // Mark all gpus as being used by the provided request
  public async markBusy(gpuIds: string[], requestId: string): Promise<void> {
    gpuIds.forEach((gpuId) => this.pendingRequests.set(gpuId, requestId));
  }

  // Find any gpu used for the provided request and mark available (remove value from map)
  public async markAvailable(requestId: string): Promise<void> {
    this.pendingRequests.forEach((value, key) => {
      if (value === requestId) {
        this.pendingRequests.set(key, undefined);
      }
    });
  }
}

export const Compute = new ComputeStatus();

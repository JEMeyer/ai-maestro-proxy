/**
 * The ComputeStatus class manages the busy status of servers, using a Map to store server IDs and their availability.
 */
class ComputeStatus {
  // "keys" are the gpu ids
  private pendingRequests: Map<string, boolean | undefined> = new Map();

  public async isBusy(gpuIds: string[]): Promise<boolean> {
    return gpuIds.some((gpuId) => this.pendingRequests.get(gpuId) || false);
  }

  public async markBusy(gpuIds: string[]): Promise<void> {
    gpuIds.forEach((gpuId) => this.pendingRequests.set(gpuId, true));
  }

  public async markAvailable(gpuIds: string[]): Promise<void> {
    gpuIds.forEach((gpuId) => this.pendingRequests.set(gpuId, false));
  }
}

export const Compute = new ComputeStatus();

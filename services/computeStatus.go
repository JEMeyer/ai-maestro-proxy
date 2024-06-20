package services

import (
	"strings"
	"sync"
)

type ComputeStatus struct {
	pendingRequests map[string]string
	mu              sync.Mutex
}

func NewComputeStatus() *ComputeStatus {
	return &ComputeStatus{
		pendingRequests: make(map[string]string),
	}
}

func (cs *ComputeStatus) IsBusy(gpuIds []string) bool {
	cs.mu.Lock()
	defer cs.mu.Unlock()
	for _, gpuId := range gpuIds {
		if _, busy := cs.pendingRequests[gpuId]; busy {
			return true
		}
	}
	return false
}

func (cs *ComputeStatus) MarkBusy(gpuIds []string, requestId string) {
	cs.mu.Lock()
	defer cs.mu.Unlock()
	for _, gpuId := range gpuIds {
		cs.pendingRequests[gpuId] = requestId
	}
}

func (cs *ComputeStatus) MarkAvailable(requestId string) {
	cs.mu.Lock()
	defer cs.mu.Unlock()
	for gpuId, reqId := range cs.pendingRequests {
		if reqId == requestId {
			delete(cs.pendingRequests, gpuId)
		}
	}

	// Check all model-specific queues for pending requests
	for _, queue := range modelQueues {
		job := queue.GetNextJob()
		if job != nil {
			gpuIds := strings.Split(job.ModelName, ",")
			if !cs.IsBusy(gpuIds) {
				cs.MarkBusy(gpuIds, job.RequestId)
				close(job.Done)
			}
		}
	}
}

var Compute = NewComputeStatus()

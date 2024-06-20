package services

import (
	"ai-maestro-proxy/models"
	"fmt"
	"strings"
)

func ReserveGPU(modelName, requestId string) (*models.ModelAssignment, []string, error) {
    if requestId == "" {
        return nil, nil, fmt.Errorf("requestId is undefined")
    }

    assignments, err := GetModelAssignments(modelName)
    if err != nil {
        return nil, nil, err
    }

    for _, assignment := range assignments {
        gpuIds := strings.Split(assignment.GpuIds, ",")
        if !Compute.IsBusy(gpuIds) {
            Compute.MarkBusy(gpuIds, requestId)
            return &assignment, gpuIds, nil
        }
    }

    modelQueue := GetModelQueue(modelName)
    done := make(chan struct{})
    modelQueue.Add(Job{ModelName: modelName, RequestId: requestId, Done: done})

    // Block until a GPU becomes available
    <-done

    // Retrieve the reserved GPUs after being notified
    assignment, gpuIds, err := GetReservedGPU(modelName, requestId)
    if err != nil || gpuIds == nil {
        return nil, nil, fmt.Errorf("failed to reserve GPU after waiting")
    }

    return assignment, gpuIds, nil
}

func GetReservedGPU(modelName, requestId string) (*models.ModelAssignment, []string, error) {
	assignments, err := GetModelAssignments(modelName)
	if err != nil {
		return nil, nil, err
	}

	for _, assignment := range assignments {
		gpuIds := strings.Split(assignment.GpuIds, ",")
		if Compute.IsBusy(gpuIds) && Compute.pendingRequests[gpuIds[0]] == requestId {
			return &assignment, gpuIds, nil
		}
	}

	return nil, nil, fmt.Errorf("no reserved GPU found for model %s and request ID %s", modelName, requestId)
}


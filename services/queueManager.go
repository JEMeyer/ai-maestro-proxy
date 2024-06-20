package services

import (
	"sync"
)

type Job struct {
	ModelName string
	RequestId string
	Done      chan struct{}
}

type Queue struct {
	jobs []Job
	mu   sync.Mutex
}

func NewQueue() *Queue {
	return &Queue{
		jobs: make([]Job, 0),
	}
}

func (q *Queue) Add(job Job) {
	q.mu.Lock()
	defer q.mu.Unlock()
	q.jobs = append(q.jobs, job)
}

func (q *Queue) GetNextJob() *Job {
	q.mu.Lock()
	defer q.mu.Unlock()
	if len(q.jobs) == 0 {
		return nil
	}
	job := q.jobs[0]
	q.jobs = q.jobs[1:]
	return &job
}

var modelQueues = make(map[string]*Queue)

func GetModelQueue(modelName string) *Queue {
	if _, exists := modelQueues[modelName]; !exists {
		modelQueues[modelName] = NewQueue()
	}
	return modelQueues[modelName]
}

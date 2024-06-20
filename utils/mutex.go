// utils/mutex.go
package utils

import (
	"sync"
)

type Mutex struct {
	mu sync.Mutex
}

func (m *Mutex) Acquire() {
	m.mu.Lock()
}

func (m *Mutex) Release() {
	m.mu.Unlock()
}

var GpuStatusMutex = &Mutex{}

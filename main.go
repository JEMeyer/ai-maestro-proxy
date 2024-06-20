package main

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net/http"
	"os"
	"time"

	"ai-maestro-proxy/db"
	"ai-maestro-proxy/services"

	"github.com/google/uuid"
	"github.com/gorilla/mux"
)

type key int

const (
	requestIDKey key = iota
	startTimeKey
)

// Custom logger that includes the request ID
type Logger struct {
	*log.Logger
}

func (l *Logger) Printf(ctx context.Context, format string, v ...interface{}) {
	requestID, ok := ctx.Value(requestIDKey).(string)
	if !ok {
		requestID = "unknown"
	}
	l.Logger.Printf(fmt.Sprintf("[RequestID: %s] %s", requestID, format), v...)
}

var logger = &Logger{log.New(os.Stdout, "", log.LstdFlags)}

type RequestBody struct {
	Model     string                 `json:"model"`
	KeepAlive *int                   `json:"keep_alive,omitempty"`
	Stream    *bool                  `json:"stream,omitempty"`
	Extras    map[string]interface{} `json:"-"`
}

func (r *RequestBody) UnmarshalJSON(data []byte) error {
	type Alias RequestBody
	aux := &struct {
		*Alias
	}{
		Alias: (*Alias)(r),
	}
	if err := json.Unmarshal(data, &aux); err != nil {
		return err
	}

	// Unmarshal the remaining fields into the Extras map
	extras := make(map[string]interface{})
	if err := json.Unmarshal(data, &extras); err != nil {
		return err
	}

	// Remove known fields from the Extras map
	delete(extras, "model")
	delete(extras, "keep_alive")
	delete(extras, "stream")

	r.Extras = extras
	return nil
}

func main() {
	// Initialize database and Redis connections
	db.InitDB()
	db.InitRedis()

	r := mux.NewRouter()
	r.Use(loggingMiddleware)
	r.Use(requestIDMiddleware)

	r.HandleFunc("/txt2img", consolidatedHandler).Methods("POST")
	r.HandleFunc("/img2img", consolidatedHandler).Methods("POST")
	r.HandleFunc("/api/generate", consolidatedHandler).Methods("POST")
	r.HandleFunc("/api/chat", consolidatedHandler).Methods("POST")
	r.HandleFunc("/api/embeddings", consolidatedHandler).Methods("POST")
	r.HandleFunc("/cache", clearCacheHandler).Methods("DELETE")

	port := "8080"
	logger.Printf(context.Background(), "Load balancer server is running on port %s", port)
	log.Fatal(http.ListenAndServe(":"+port, r))
}

func loggingMiddleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		ctx := r.Context()
		startTime := time.Now()
		ctx = context.WithValue(ctx, startTimeKey, startTime)
		logger.Printf(ctx, "%s %s", r.Method, r.RequestURI)
		next.ServeHTTP(w, r.WithContext(ctx))
	})
}

func requestIDMiddleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		requestID := uuid.New().String()
		ctx := context.WithValue(r.Context(), requestIDKey, requestID)
		r.Header.Set("X-Request-ID", requestID)
		next.ServeHTTP(w, r.WithContext(ctx))
	})
}

func consolidatedHandler(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	logger.Printf(ctx, "Received request: %s %s", r.Method, r.URL.Path)

	if r.Header.Get("Content-Type") != "application/json" {
		logger.Printf(ctx, "Invalid Content-Type: %s", r.Header.Get("Content-Type"))
		http.Error(w, "Content-Type must be application/json", http.StatusUnsupportedMediaType)
		return
	}

	var reqBody RequestBody
	err := json.NewDecoder(r.Body).Decode(&reqBody)
	if err != nil {
		logger.Printf(ctx, "Error decoding request body: %v", err)
		http.Error(w, "Invalid request body", http.StatusBadRequest)
		return
	}

	modelName := reqBody.Model
	requestID := r.Header.Get("X-Request-ID")

	// Merge known fields with extra fields
	mergedBody := make(map[string]interface{})
	mergedBody["model"] = reqBody.Model
	if reqBody.KeepAlive != nil {
		mergedBody["keep_alive"] = *reqBody.KeepAlive
	}
	if reqBody.Stream != nil {
		mergedBody["stream"] = *reqBody.Stream
	}
	for k, v := range reqBody.Extras {
		mergedBody[k] = v
	}

	bodyBytes, err := json.Marshal(mergedBody)
	if err != nil {
		logger.Printf(ctx, "Error marshaling merged body: %v", err)
		http.Error(w, "Error processing request", http.StatusInternalServerError)
		return
	}

	r.Body = io.NopCloser(bytes.NewBuffer(bodyBytes))
	r.ContentLength = int64(len(bodyBytes))

	// Cancel this request after 20 seconds
	ctx, cancel := context.WithTimeout(r.Context(), 20*time.Second)
	defer cancel()

	result, gpuIds, done, err := services.ReserveGPU(modelName, requestID)
	if err != nil {
		logger.Printf(ctx, "Error reserving GPU: %v", err)
		http.Error(w, err.Error(), http.StatusInternalServerError)
		return
	}

	// Log the acquired GPU IDs
	if gpuIds != nil {
		logger.Printf(ctx, "Acquired GPU IDs: %v", gpuIds)
	}

	go func() {
		select {
		case <-done:
			result, gpuIds, err = services.GetReservedGPU(modelName, requestID)
			if err != nil {
				logger.Printf(ctx, "Error getting reserved GPU: %v", err)
			} else {
				logger.Printf(ctx, "Reserved GPU(s): %v", gpuIds)
			}
		case <-ctx.Done():
			logger.Printf(ctx, "Timeout waiting for GPU")
		}
	}()

	// Cleanup function we defer to make sure we don't get gpus stuck in busy state
	defer func() {
		if gpuIds != nil {
			unlockedGpuIds := services.Compute.MarkAvailable(requestID)
			logger.Printf(ctx, "Marked GPU(s) as available: %v", unlockedGpuIds)
		}
	}()

	if result == nil {
		select {
		case <-done:
			result, gpuIds, err = services.GetReservedGPU(modelName, requestID)
			if err != nil {
				logger.Printf(ctx, "Error getting reserved GPU: %v", err)
				http.Error(w, err.Error(), http.StatusInternalServerError)
				return
			}
			logger.Printf(ctx, "Reserved GPU(s): %v", gpuIds)
		case <-ctx.Done():
			logger.Printf(ctx, "Timeout waiting for GPU")
			http.Error(w, "Timeout waiting for GPU", http.StatusGatewayTimeout)
			return
		}
	}

	proxyStartTime := time.Now()
	proxyURL := fmt.Sprintf("http://%s:%d%s", result.IpAddr, result.Port, r.RequestURI)
	logger.Printf(ctx, "Proxying request to: %s", proxyURL)

	if reqBody.Stream != nil && *reqBody.Stream {
		resp, err := http.Post(proxyURL, "application/json", r.Body)
		if err != nil {
			logger.Printf(ctx, "Error proxying request: %v", err)
			http.Error(w, "Error proxying request", http.StatusInternalServerError)
			return
		}
		defer resp.Body.Close()

		w.Header().Set("Content-Type", resp.Header.Get("Content-Type"))
		w.WriteHeader(resp.StatusCode)

		flusher, ok := w.(http.Flusher)
		if !ok {
			logger.Printf(ctx, "Streaming not supported")
			http.Error(w, "Streaming not supported", http.StatusInternalServerError)
			return
		}

		buf := make([]byte, 4096)
		for {
			n, err := resp.Body.Read(buf)
			if err != nil && err != io.EOF {
				logger.Printf(ctx, "Error reading response: %v", err)
				http.Error(w, "Error streaming response", http.StatusInternalServerError)
				break
			} else if n == 0 {
				break
			}

			_, err = w.Write(buf[:n])
			if err != nil {
				logger.Printf(ctx, "Error writing to response: %v", err)
				http.Error(w, "Error streaming response", http.StatusInternalServerError)
				break
			}

			flusher.Flush()
		}
	} else {
		resp, err := http.Post(proxyURL, "application/json", r.Body)
		if err != nil {
			logger.Printf(ctx, "Error proxying request: %v", err)
			http.Error(w, "Error proxying request", http.StatusInternalServerError)
			return
		}
		defer resp.Body.Close()

		body, err := io.ReadAll(resp.Body)
		if err != nil {
			logger.Printf(ctx, "Error reading response body: %v", err)
			http.Error(w, "Error reading response body", http.StatusInternalServerError)
			return
		}

		w.WriteHeader(resp.StatusCode)
		_, err = w.Write(body)
		if err != nil {
			logger.Printf(ctx, "Error writing response: %v", err)
			http.Error(w, "Error writing response", http.StatusInternalServerError)
		}
	}

	proxyDuration := time.Since(proxyStartTime)
	logger.Printf(ctx, "Proxy request duration: %v", proxyDuration)

	totalDuration := time.Since(ctx.Value(startTimeKey).(time.Time))
	logger.Printf(ctx, "Total request duration: %v", totalDuration)
}

func clearCacheHandler(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	err := services.DeleteModelAssignmentCache()
	if err != nil {
		logger.Printf(ctx, "Failed to clear cache")
		http.Error(w, "Failed to clear cache", http.StatusInternalServerError)
		return
	}
	w.WriteHeader(http.StatusOK)
	w.Write([]byte(`{"message": "Cache cleared successfully"}`))
	logger.Printf(ctx, "Cache cleared successfully")
}

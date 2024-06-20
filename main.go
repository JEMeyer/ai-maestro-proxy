package main

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net/http"
	"time"

	"ai-maestro-proxy/db"
	"ai-maestro-proxy/services"

	"github.com/google/uuid"
	"github.com/gorilla/mux"
)

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
	log.Printf("Load balancer server is running on port %s", port)
	log.Fatal(http.ListenAndServe(":"+port, r))
}

func loggingMiddleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		log.Printf("%s %s", r.Method, r.RequestURI)
		next.ServeHTTP(w, r)
	})
}

func requestIDMiddleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		requestID := uuid.New().String()
		r.Header.Set("X-Request-ID", requestID)
		next.ServeHTTP(w, r)
	})
}

func consolidatedHandler(w http.ResponseWriter, r *http.Request) {
	log.Println("Received request:", r.Method, r.URL.Path)

	if r.Header.Get("Content-Type") != "application/json" {
		log.Println("Invalid Content-Type:", r.Header.Get("Content-Type"))
		http.Error(w, "Content-Type must be application/json", http.StatusUnsupportedMediaType)
		return
	}

	var reqBody RequestBody
	err := json.NewDecoder(r.Body).Decode(&reqBody)
	if err != nil {
		log.Printf("Error decoding request body: %v", err)
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
		log.Printf("Error marshaling merged body: %v", err)
		http.Error(w, "Error processing request", http.StatusInternalServerError)
		return
	}

	r.Body = io.NopCloser(bytes.NewBuffer(bodyBytes))
	r.ContentLength = int64(len(bodyBytes))

	// Cancel this request after 20 seconds
	ctx, cancel := context.WithTimeout(r.Context(), 20*time.Second)
	defer cancel()

	result, gpus, done, err := services.ReserveGPU(modelName, requestID)
	if err != nil {
		log.Printf("Error reserving GPU: %v", err)
		http.Error(w, err.Error(), http.StatusInternalServerError)
		return
	}

	go func() {
		select {
		case <-done:
			result, gpus, err = services.GetReservedGPU(modelName, requestID)
			log.Printf("Reserved GPU(s) %v", err)
			if err != nil {
				log.Printf("Error getting reserved GPU: %v", err)
			}
		case <-ctx.Done():
			log.Println("Timeout waiting for GPU")
		}
	}()

	// Cleanup function we defer to make sure we don't get gpus stuck in busy state
	defer func() {
		services.Compute.MarkAvailable(requestID)
		log.Println("Marked GPU as available for request ID:", requestID)
	}()

	if result == nil {
		select {
		case <-done:
			result, gpus, err = services.GetReservedGPU(modelName, requestID)
			if err != nil {
				log.Printf("Error getting reserved GPU: %v", err)
				http.Error(w, err.Error(), http.StatusInternalServerError)
				return
			}
		case <-ctx.Done():
			log.Println("Timeout waiting for GPU")
			http.Error(w, "Timeout waiting for GPU", http.StatusGatewayTimeout)
			return
		}
	}

	proxyURL := fmt.Sprintf("http://%s:%d%s", result.IpAddr, result.Port, r.RequestURI)
	log.Println("Proxying request to:", proxyURL)

	if reqBody.Stream != nil && *reqBody.Stream {
		resp, err := http.Post(proxyURL, "application/json", r.Body)
		if err != nil {
			log.Printf("Error proxying request: %v", err)
			http.Error(w, "Error proxying request", http.StatusInternalServerError)
			return
		}
		defer resp.Body.Close()

		w.Header().Set("Content-Type", resp.Header.Get("Content-Type"))
		w.WriteHeader(resp.StatusCode)

		flusher, ok := w.(http.Flusher)
		if !ok {
			log.Println("Streaming not supported")
			http.Error(w, "Streaming not supported", http.StatusInternalServerError)
			return
		}

		buf := make([]byte, 4096)
		for {
			n, err := resp.Body.Read(buf)
			if err != nil && err != io.EOF {
				log.Printf("Error reading response: %v", err)
				http.Error(w, "Error streaming response", http.StatusInternalServerError)
				break
			} else if n == 0 {
				break
			}

			_, err = w.Write(buf[:n])
			if err != nil {
				log.Printf("Error writing to response: %v", err)
				http.Error(w, "Error streaming response", http.StatusInternalServerError)
				break
			}

			flusher.Flush()
		}
	} else {
		resp, err := http.Post(proxyURL, "application/json", r.Body)
		if err != nil {
			log.Printf("Error proxying request: %v", err)
			http.Error(w, "Error proxying request", http.StatusInternalServerError)
			return
		}
		defer resp.Body.Close()

		body, err := io.ReadAll(resp.Body)
		if err != nil {
			log.Printf("Error reading response body: %v", err)
			http.Error(w, "Error reading response body", http.StatusInternalServerError)
			return
		}

		w.WriteHeader(resp.StatusCode)
		_, err = w.Write(body)
		if err != nil {
			log.Printf("Error writing response: %v", err)
			http.Error(w, "Error writing response", http.StatusInternalServerError)
		}
	}
}

func clearCacheHandler(w http.ResponseWriter, r *http.Request) {
	err := services.DeleteModelAssignmentCache()
	if err != nil {
		http.Error(w, "Failed to clear cache", http.StatusInternalServerError)
		return
	}
	w.WriteHeader(http.StatusOK)
	w.Write([]byte(`{"message": "Cache cleared successfully"}`))
}

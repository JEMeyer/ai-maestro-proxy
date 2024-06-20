#!/bin/bash

# Load environment variables from .env file, ignoring comments
if [ -f .env ]; then
    export $(grep -v '^#' .env | xargs)
fi

# Run the Go application
go run main.go

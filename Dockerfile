# Use a minimal Node.js base image
FROM node:18-alpine as base

# Install PM2 globally
RUN npm install -g pm2

# Builder stage
FROM base AS builder

# Set the working directory inside the container
WORKDIR /app

# Copy package.json and package-lock.json
COPY package*.json ./

# Install dependencies
RUN npm ci

# Copy the rest of the project files
COPY . .

# Build the TypeScript code to JavaScript
RUN npm run build

# Final runtime image
FROM base

# Set the working directory inside the container
WORKDIR /app

# Copy the package.json and package-lock.json files from the builder image
COPY --from=builder /app/package*.json ./

# Copy the built JavaScript files from the builder image
COPY --from=builder /app/dist ./dist

# Install production dependencies
RUN npm ci --only=production

# Proxy port
EXPOSE 11434

# Start the application using PM2
CMD ["pm2-runtime", "dist/proxy.js"]

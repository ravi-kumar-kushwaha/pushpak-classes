# Multi-stage Dockerfile for PushpakClasses Web (Angular SPA on Nginx)
# Stage 1: Build Angular application
FROM node:22-alpine AS build
WORKDIR /app

# Copy dependency manifests
COPY pushpak-classes/package*.json ./
RUN npm install

# Copy application source code
COPY pushpak-classes/ .

# Build production bundle
RUN npm run build -- --configuration=production

# Stage 2: Serve with Nginx Alpine
FROM nginx:alpine AS runtime

# Copy build artifacts to Nginx html directory
COPY --from=build /app/dist/pushpak-classes/browser /usr/share/nginx/html

# Copy custom Nginx configuration with SPA routing fallback
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]

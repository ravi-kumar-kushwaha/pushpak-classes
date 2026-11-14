# Multi-stage Dockerfile for PushpakClasses Web (Context: frontend/pushpak-classes)
FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm install

COPY . .
RUN npm run build -- --configuration=production

FROM nginx:alpine AS runtime
COPY --from=build /app/dist/pushpak-classes/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]

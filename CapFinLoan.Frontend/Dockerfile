# ── Stage 1: Build React app ──────────────────────────────────────────────────
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files first — cached until package.json changes
COPY package.json yarn.lock* package-lock.json* ./
RUN npm install --prefer-offline

# Copy source and build
COPY . .

# Inject the gateway URL at build time
# In Docker, the gateway is exposed on port 5000 — browser calls it directly
ARG VITE_GATEWAY_URL=http://localhost:5000
ENV VITE_GATEWAY_URL=${VITE_GATEWAY_URL}
ENV VITE_DOCUMENT_API_URL=${VITE_GATEWAY_URL}
ENV VITE_APPLICATION_API_URL=${VITE_GATEWAY_URL}
ENV VITE_AUTH_API_URL=${VITE_GATEWAY_URL}
ENV VITE_SIGNALR_HUB_URL=http://localhost:5000/gateway/hubs/document-status

RUN npm run build

# ── Stage 2: Serve with Nginx ─────────────────────────────────────────────────
FROM nginx:1.27-alpine AS runtime

# Remove default config
RUN rm /etc/nginx/conf.d/default.conf

COPY nginx.conf /etc/nginx/conf.d/app.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]

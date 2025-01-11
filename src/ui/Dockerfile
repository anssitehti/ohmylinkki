FROM oven/bun:1 AS base
WORKDIR /app
EXPOSE 80

FROM oven/bun:1 AS build
WORKDIR /src
COPY package.json bun.lockb ./
RUN bun install --frozen-lockfile
COPY . .
RUN bunx --bun vite build

FROM base AS final
WORKDIR /app
COPY --from=build /src/dist /app/dist

RUN bun add -g sirv-cli
ENTRYPOINT [ "bun", "run", "sirv", "dist", "--single", "--cors", "--brotli", "--gzip", "--port", "80", "--host" ]
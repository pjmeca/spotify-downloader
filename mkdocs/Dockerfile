FROM python:3.12-slim AS base
WORKDIR /app
RUN apt-get update \
     # for image optimization
     && apt-get install -y --no-install-recommends pngquant \
     && rm -rf /var/lib/apt/lists/*
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY . .
EXPOSE 80

# -------- dev target --------
FROM base AS dev
ENTRYPOINT ["mkdocs", "serve", "-a", "0.0.0.0:80", "--livereload"]

# -------- build target --------
FROM base AS build
RUN mkdocs build --clean

# -------- prod target (nginx) --------
FROM nginx:alpine AS prod
COPY --from=build /app/site/ /usr/share/nginx/html/

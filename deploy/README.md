# Deploying Tollgate Server

This guide covers the canonical deployment patterns. Pick the one that matches your infrastructure.

---

## 1. Docker Compose (recommended for getting started)

```bash
cd deploy/docker
cp .env.example .env
nano .env   # set TOLLGATE_JWT_SECRET, TOLLGATE_ADMIN_KEY and TOLLGATE_CORS_ORIGINS
docker compose up -d --build
```

- Server: `http://localhost:7431`
- Swagger: Development only (the server does not enable it in Production)
- SQLite DB persisted in the `tollgate-data` Docker volume (`/data/licenses.db`)
- The healthcheck (curl is installed in the image) turns the service `healthy` once the API responds

> `TOLLGATE_CORS_ORIGINS` is **required** in Production — the server refuses
> to start with an empty CORS allow-list.

---

## 2. Linux VPS (systemd + nginx + Let's Encrypt)

### 2.1 Install .NET 10 runtime

```bash
# Ubuntu 24.04 example
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update && sudo apt-get install -y dotnet-runtime-10.0 aspnetcore-runtime-10.0
```

### 2.2 Publish & install the server

```bash
git clone https://github.com/your-org/tollgate.git
cd tollgate
dotnet publish src/Tollgate.Server -c Release -o ./publish/server

sudo mkdir -p /opt/tollgate/data
sudo cp -r publish/server/* /opt/tollgate/
sudo chown -R www-data:www-data /opt/tollgate
```

The systemd unit sets `Data__Path=/opt/tollgate/data/licenses.db` so the
database lives in the writable `data` directory (surviving upgrades), not
inside the read-only-strict application tree.

### 2.3 Install the systemd unit

```bash
sudo cp deploy/systemd/tollgate.service /etc/systemd/system/
# Edit the file: replace the Jwt__Secret and Admin__Key env vars,
# and set Cors__AllowedOrigins__0 to your app's origin!
sudo nano /etc/systemd/system/tollgate.service

sudo systemctl daemon-reload
sudo systemctl enable --now tollgate
sudo systemctl status tollgate
journalctl -u tollgate -f
```

Test: `curl http://localhost:7431/api/license/health` → `{"status":"ok",...}`.

### 2.4 Set up nginx + HTTPS

```bash
sudo apt install -y nginx certbot python3-certbot-nginx

# Edit deploy/nginx/tollgate.conf: replace license.yourapp.com with your domain.
sudo cp deploy/nginx/tollgate.conf /etc/nginx/sites-available/
sudo ln -s /etc/nginx/sites-available/tollgate.conf /etc/nginx/sites-enabled/

# Get a free TLS cert from Let's Encrypt:
sudo certbot --nginx -d license.yourapp.com

sudo nginx -t
sudo systemctl reload nginx
```

---

## 3. Secrets rotation

### Rotate `Jwt:Secret`

1. Generate a new secret: `openssl rand -base64 48`.
2. Update it on the server (`appsettings.json` or env var).
3. Update it on every client (`TollgateOptions.SharedSecret`).
4. Restart the server. Cached JWTs become invalid; clients re-validate automatically.

For zero-downtime rotation, use RSA signing instead (see main README).

### Rotate `Admin:Key`

1. Change `Admin:Key` in `appsettings.json` or env var.
2. Restart the server. Existing license keys are unaffected.
3. Update the KeyGen CLI's saved admin key (or use `tollgate-keygen init`).

---

## Backups

- **SQLite**: back up the database on a schedule. The file lives at the
  `Data:Path` location (`/data/licenses.db` in Docker, `/opt/tollgate/data/licenses.db`
  under systemd, next to the binary otherwise):
  ```bash
  sqlite3 /opt/tollgate/data/licenses.db ".backup '/backup/licenses-$(date +%F).db'"
  ```
- **Postgres/SQL Server**: use your DB's standard backup tooling.

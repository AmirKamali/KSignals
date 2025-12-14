# RabbitMQ Management UI Setup

## Problem
Accessing `https://kalshisignals.com:15672` returns 404 because Traefik (the reverse proxy) is not configured to listen on port 15672.

## Solution

To enable access to RabbitMQ Management UI on `https://kalshisignals.com:15672`, you need to configure Traefik to:
1. Listen on port 15672
2. Forward traffic to the RabbitMQ container

### Step 1: Update Traefik Configuration

Add a new entrypoint for RabbitMQ in your Traefik static configuration (usually in `traefik.yml` or `traefik.toml`):

#### If using YAML (`traefik.yml`):
```yaml
entryPoints:
  web:
    address: ":80"
  websecure:
    address: ":443"
  rabbitmq:
    address: ":15672"
```

#### If using TOML (`traefik.toml`):
```toml
[entryPoints]
  [entryPoints.web]
    address = ":80"
  [entryPoints.websecure]
    address = ":443"
  [entryPoints.rabbitmq]
    address = ":15672"
```

### Step 2: Update Traefik Docker Configuration

Ensure Traefik container exposes port 15672. In your Traefik `docker-compose.yml`:

```yaml
services:
  traefik:
    # ... other config ...
    ports:
      - "80:80"
      - "443:443"
      - "15672:15672"  # Add this line
```

### Step 3: Restart Traefik

After updating the configuration:
```bash
docker-compose restart traefik
# or
docker restart traefik
```

### Step 4: Restart RabbitMQ Service

After Traefik is configured:
```bash
cd /path/to/kalshi-signals
docker-compose restart rabbitmq
```

## Verification

1. Check that RabbitMQ container is running:
   ```bash
   docker ps | grep rabbitmq
   ```

2. Check Traefik logs for any errors:
   ```bash
   docker logs traefik
   ```

3. Access the management UI:
   ```
   https://kalshisignals.com:15672
   ```

4. Default credentials (if not changed):
   - Username: `guest`
   - Password: `guest`

## Firewall Configuration

If you're still getting connection refused or timeout errors after the above steps, ensure your firewall allows incoming traffic on port 15672:

```bash
# For UFW (Ubuntu)
sudo ufw allow 15672/tcp

# For firewalld (CentOS/RHEL)
sudo firewall-cmd --permanent --add-port=15672/tcp
sudo firewall-cmd --reload
```

## Alternative: Access Without Custom Port

If you don't want to configure Traefik for port 15672, you can access RabbitMQ management UI through SSH port forwarding:

```bash
ssh -L 15672:localhost:15672 user@kalshisignals.com
```

Then access it locally at: `http://localhost:15672`

## Current Configuration

The `docker-compose.yml` has been updated with:
- RabbitMQ added to the `web` network (required for Traefik)
- Traefik labels configured to route traffic from the `rabbitmq` entrypoint
- TLS/SSL enabled with Let's Encrypt certificate resolver

Once Traefik is configured with the `rabbitmq` entrypoint, the setup will be complete.

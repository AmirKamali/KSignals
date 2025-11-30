# ClickHouse Setup

This directory contains the Docker Compose configuration for running ClickHouse.

## Credentials

- **Username**: qv6t0lSQNqaaKPEpxazLcZjoS
- **Password**: xXnBtibbHu5be6U1k1X9IBxie
- **Database**: kalshi_signals
- **Port**: 8123 (HTTP), 9000 (Native)

## Running

```bash
docker compose up -d
```

## Connection String Format

```
Host=localhost;Port=9000;Database=kalshi_signals;User=qv6t0lSQNqaaKPEpxazLcZjoS;Password=xXnBtibbHu5be6U1k1X9IBxie
```

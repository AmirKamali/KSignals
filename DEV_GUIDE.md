# Kalshi Signals - Development Guide

## Quick Start

### Running the Development Environment

The easiest way to run both the backend and frontend with hot reload is to use the development script:

```bash
./run_dev.sh
```

This will:
- ✅ Check if required ports (3011, 3006) are available
- ✅ Start the backend API on `http://localhost:3006` with hot reload
- ✅ Start the frontend on `http://localhost:3011` with hot reload
- ✅ Set up proper environment variables
- ✅ Create and stream logs to `logs/backend.log` and `logs/frontend.log`
- ✅ Automatically stop all services when you press `Ctrl+C`

### Service URLs

Once running, you can access:

| Service | URL |
|---------|-----|
| **Frontend** | http://localhost:3011 |
| **Backend API** | http://localhost:3006 |
| **Swagger Docs** | http://localhost:3006/swagger |

### Viewing Logs

Logs are automatically tailed in the terminal, but you can also view them separately:

```bash
# Backend logs
tail -f logs/backend.log

# Frontend logs
tail -f logs/frontend.log
```

## Manual Development

If you need to run services individually:

### Backend Only

```bash
cd backend/KSignal.API
dotnet watch run --urls "http://localhost:3006"
```

### Frontend Only

```bash
cd web
export BACKEND_API_BASE_URL="http://localhost:3006"
dotnet watch run
```

## Authentication Setup

The application uses Firebase for authentication with Google Sign-In.

### Login Flow

1. User clicks "Login" button → navigates to `/Login`
2. User clicks "Sign in with Google" → Firebase popup opens
3. After authentication:
   - User is synced with backend (`/api/users/register`)
   - Backend generates JWT token (`/api/users/login`)
   - User is redirected back to home page
   - Login button is hidden, username is displayed

### Firebase Configuration

Firebase config is located in:
- `web/wwwroot/js/firebase-init.js`

## Project Structure

```
kalshi-signals/
├── backend/
│   └── KSignal.API/          # Backend API (ASP.NET Core)
├── web/                       # Frontend (ASP.NET Core Razor Pages)
│   ├── Pages/                 # Razor pages
│   ├── wwwroot/              # Static files
│   │   ├── css/              # Stylesheets
│   │   └── js/               # JavaScript files
│   └── Services/             # Backend client services
├── shared/                    # Shared DTOs
├── logs/                      # Development logs
└── run_dev.sh                # Development script
```

## Troubleshooting

### Port Already in Use

If you get a "port already in use" error:

```bash
# Use the kill_backend script to kill processes on port 3006
./kill_backend.sh

# Kill all dotnet processes
killall -9 dotnet

# Or kill specific ports manually
lsof -ti :3011 | xargs kill -9
lsof -ti :3006 | xargs kill -9
```

### Firebase Not Loading

Check browser console for errors. Make sure:
1. Firebase scripts are loaded in `_Layout.cshtml`
2. `window.backendBaseUrl` is set correctly
3. Backend API is running and accessible

### Hot Reload Not Working

```bash
# Stop services (Ctrl+C in run_dev.sh terminal)
# Clear bin/obj directories
rm -rf web/bin web/obj backend/KSignal.API/bin backend/KSignal.API/obj
# Restart
./run_dev.sh
```

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `BACKEND_API_BASE_URL` | `http://localhost:3006` | Backend API base URL |
| `JWT_SECRET` | `dev-secret-key-...` | Secret key for JWT token signing (min 32 chars) |
| `ASPNETCORE_ENVIRONMENT` | `Development` | ASP.NET environment |

## Database

The application uses Entity Framework Core with a SQLite database (or your configured provider).

To apply migrations:

```bash
cd backend/KSignal.API
dotnet ef database update
```

## Stopping Services

When using `run_dev.sh`, simply press **Ctrl+C** to stop all services gracefully.

The script will automatically:
- Kill both backend and frontend processes
- Clean up any remaining dotnet watch processes
- Exit cleanly

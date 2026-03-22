# Environment Configuration

This project uses **Environment Variables** for configuration. There are three priority levels:

1. **Environment Variables** (Highest - Used in Production/K8s)
2. **.env file** (Development - Local override)
3. **appsettings.json** (Lowest - Fallback defaults)

## Local Development Setup

1. Copy `.env.example` to `.env`:
   ```bash
   cp .env.example .env
   ```

2. Edit `.env` with your actual credentials:
   ```bash
   nano .env  # or use any text editor
   ```

3. The `.env` file is automatically loaded on startup and **ignored by git**.

## Production (Kubernetes)

In production, set these as **Kubernetes Secrets** or **Environment Variables**:

```yaml
env:
  - name: DB_SERVER
    valueFrom:
      secretKeyRef:
        name: auctionhub-secrets
        key: db-server
  - name: DB_PASSWORD
    valueFrom:
      secretKeyRef:
        name: auctionhub-secrets
        key: db-password
  # ... etc
```

## Available Environment Variables

See `.env.example` for the complete list.

### Required Variables:
- `DB_SERVER` - Database server address
- `DB_NAME` - Database name
- `DB_USER` - Database username
- `DB_PASSWORD` - Database password
- `REDIS_URL` - Redis connection string

### Optional Variables:
- `ADMIN_EMAIL` - Admin account email (default: admin@auctionhub.com)
- `ADMIN_PASSWORD` - Admin account password (default: Admin123!)
- `CLOUDINARY_*` - Image upload service credentials
- `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET` - Google OAuth
- `GITHUB_CLIENT_ID`, `GITHUB_CLIENT_SECRET` - GitHub OAuth
- `EMAIL_*` - SMTP email configuration

## Security Notes

- ⚠️ **NEVER commit `.env` file to git** (already in `.gitignore`)
- ⚠️ **Change default passwords** in production
- ✅ `appsettings.json` contains only **placeholder values** safe for git
- ✅ Production uses **Kubernetes Secrets** (not appsettings.json)

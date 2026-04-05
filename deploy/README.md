# AuctionHub Hosting Guide (k3s)

This guide describes the steps to deploy AuctionHub into a k3s Kubernetes cluster.

## Directory Structure
- `auctionhub-web.yaml` - Deployment, Service, and Ingress definitions for the web application.
- `mssql-db.yaml` - Deployment, Service, and PVC definitions for the SQL Server.
- `secrets-setup.sh` - Shell script template to create the necessary Secrets in the cluster.

---

## Installation Steps

### 1. Create Secrets
Before deploying the applications, you must generate the sensitive keys. Open `secrets-setup.sh`, fill in your actual values, and execute it:
```bash
chmod +x secrets-setup.sh
./secrets-setup.sh
```

### 2. Deploy the Database (MSSQL)
Run the command to start the SQL Server:
```bash
kubectl apply -f mssql-db.yaml
```

### 3. Configure SQL Server User
Once the SQL pod is in `Running` state, you need to create the application-specific user:
```bash
kubectl exec -it $(kubectl get pod -l app=mssql -o jsonpath="{.items[0].metadata.name}") -- /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YOUR_SA_PASSWORD' -C -No -Q "IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'auction_user') CREATE LOGIN auction_user WITH PASSWORD = 'YOUR_DB_PASSWORD'; USE AuctionHubDb; IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'auction_user') CREATE USER auction_user FOR LOGIN auction_user; ALTER ROLE db_owner ADD MEMBER auction_user;"
```

### 4. Deploy the Web Application
Start the main application:
```bash
kubectl apply -f auctionhub-web.yaml
```

### 5. Verification
Verify that all pods are running correctly:
```bash
kubectl get pods
kubectl get ingress
```

---

## Requirements
- An active k3s cluster.
- Traefik Ingress Controller (enabled by default in k3s).
- Cert-manager for automatic SSL certificate issuance (optional but recommended).

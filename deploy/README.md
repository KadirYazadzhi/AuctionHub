# AuctionHub Kubernetes Deployment Guide

This repository contains the necessary manifests and scripts to host the **AuctionHub** application on a Kubernetes cluster (e.g., k3s, Minikube, or a production-grade cluster).

## Deployment Files
- `auctionhub-web.yaml`: Contains the Deployment, Service, and Ingress for the ASP.NET Core web application.
- `mssql-db.yaml`: Contains the PersistentVolumeClaim (PVC), Deployment, and Service for the Microsoft SQL Server database.
- `secrets-setup.sh`: A helper script to create the required Kubernetes Secrets with your environment-specific credentials.

---

## Step-by-Step Installation

### 1. Configure and Create Secrets
The application relies on several sensitive values (API keys, passwords, connection strings).
1. Open `secrets-setup.sh` in a text editor.
2. Replace all `<PLACEHOLDER>` values with your actual configuration.
3. Make the script executable and run it:
   ```bash
   chmod +x secrets-setup.sh
   ./secrets-setup.sh
   ```

### 2. Deploy the Database (MSSQL)
Apply the database manifests to create the persistent storage and start the SQL Server:
```bash
kubectl apply -f mssql-db.yaml
```
*Note: It may take a minute for the pod to reach the `Running` state as it initializes the storage.*

### 3. Initialize the Database User
After the SQL Server pod is running, you must create the application-specific user and grant permissions. Run the following command (replace `<SA_PASSWORD>` and `<DB_PASSWORD>` with the values used in `secrets-setup.sh`):

```bash
kubectl exec -it $(kubectl get pod -l app=mssql -o jsonpath="{.items[0].metadata.name}") -- /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '<SA_PASSWORD>' -C -No -Q "USE AuctionHubDb; IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'auction_user') CREATE USER auction_user FOR LOGIN auction_user; ALTER ROLE db_owner ADD MEMBER auction_user;"
```

### 4. Deploy the Web Application
Once the database is ready, deploy the main application. The `initContainer` in this manifest will ensure the application waits for the SQL Server port to be accessible before starting.
```bash
kubectl apply -f auctionhub-web.yaml
```

### 5. Verification and Access
Check the status of your pods and the Ingress resource:
```bash
kubectl get pods
kubectl get ingress
```
Once the Ingress has an assigned IP, you can access the application at the domain specified in your `auctionhub-web.yaml`.

---

## Infrastructure Requirements
- **Kubernetes Cluster**: Version 1.24 or higher recommended.
- **Ingress Controller**: This configuration is optimized for **Traefik** (with sticky session support).
- **SSL/TLS**: The Ingress manifest assumes the use of `cert-manager` for certificate management. Update the `cert-manager.io/cluster-issuer` annotation as needed.
- **Storage**: A dynamic provisioner or a manual PV must be available to satisfy the 5Gi PVC request for MSSQL.

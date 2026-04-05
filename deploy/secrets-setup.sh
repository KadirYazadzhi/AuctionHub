#!/bin/bash

# --- CONFIGURATION ---
# Fill in your real values here:
SA_PASSWORD='YOUR_STRONG_SA_PASSWORD'
DB_USER='auction_user'
DB_PASSWORD='YOUR_DB_PASSWORD'
REDIS_URL='YOUR_REDIS_CONNECTION_STRING' # e.g., redis-master:6379,password=...
CLOUDINARY_CLOUDNAME='YOUR_CLOUDNAME'
CLOUDINARY_APIKEY='YOUR_APIKEY'
CLOUDINARY_APISECRET='YOUR_APISECRET'
GOOGLE_CLIENTID='YOUR_GOOGLE_ID'
GOOGLE_CLIENTSECRET='YOUR_GOOGLE_SECRET'
GITHUB_CLIENTID='YOUR_GITHUB_ID'
GITHUB_CLIENTSECRET='YOUR_GITHUB_SECRET'
EMAIL_APITOKEN='YOUR_EMAIL_TOKEN'
EMAIL_PASSWORD='YOUR_EMAIL_PASSWORD'
HF_TOKEN='YOUR_HUGGINGFACE_TOKEN'
RECAPTCHA_SITEKEY='YOUR_RECAPTCHA_SITEKEY'
RECAPTCHA_SECRETKEY='YOUR_RECAPTCHA_SECRETKEY'
ADMIN_PASSWORD='YOUR_INITIAL_ADMIN_PASSWORD'
ADMIN_FIRST_NAME='System'
ADMIN_LAST_NAME='Admin'

# Generate Default Connection String for the application
DEFAULT_CONNECTION="Server=mssql-svc,1433;Database=AuctionHubDb;User Id=$DB_USER;Password=$DB_PASSWORD;TrustServerCertificate=True;Encrypt=False;Connect Timeout=60;"

# --- EXECUTION ---
kubectl create secret generic auctionhub-secrets \
  --from-literal=SA_PASSWORD="$SA_PASSWORD" \
  --from-literal=DB_USER="$DB_USER" \
  --from-literal=DB_PASSWORD="$DB_PASSWORD" \
  --from-literal=ConnectionStrings__DefaultConnection="$DEFAULT_CONNECTION" \
  --from-literal=redis-connection="$REDIS_URL" \
  --from-literal=ConnectionStrings__Redis="$REDIS_URL" \
  --from-literal=cloudinary-cloudname="$CLOUDINARY_CLOUDNAME" \
  --from-literal=cloudinary-apikey="$CLOUDINARY_APIKEY" \
  --from-literal=cloudinary-apisecret="$CLOUDINARY_APISECRET" \
  --from-literal=google-clientid="$GOOGLE_CLIENTID" \
  --from-literal=google-clientsecret="$GOOGLE_CLIENTSECRET" \
  --from-literal=github-clientid="$GITHUB_CLIENTID" \
  --from-literal=github-clientsecret="$GITHUB_CLIENTSECRET" \
  --from-literal=email-apitoken="$EMAIL_APITOKEN" \
  --from-literal=EmailSettings__Password="$EMAIL_PASSWORD" \
  --from-literal=AI__HuggingFaceToken="$HF_TOKEN" \
  --from-literal=GoogleReCaptcha__SiteKey="$RECAPTCHA_SITEKEY" \
  --from-literal=GoogleReCaptcha__SecretKey="$RECAPTCHA_SECRETKEY" \
  --from-literal=ADMIN_PASSWORD="$ADMIN_PASSWORD" \
  --from-literal=ADMIN_FIRST_NAME="$ADMIN_FIRST_NAME" \
  --from-literal=ADMIN_LAST_NAME="$ADMIN_LAST_NAME"

echo "Secrets created successfully!"

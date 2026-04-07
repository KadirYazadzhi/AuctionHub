#!/bin/bash

# --- CONFIGURATION ---
# Replace the placeholders below with your actual sensitive data.
# Do not commit the filled version of this file to source control.

# Database Credentials
SA_PASSWORD='<STRONG_SA_PASSWORD>'
DB_USER='auction_user'
DB_PASSWORD='<STRONG_DB_PASSWORD>'
DB_NAME='AuctionHubDb'

# Connection Strings
# Replace <REDIS_HOST> and <REDIS_PASSWORD> with your Redis details
REDIS_CONNECTION='<REDIS_HOST>:<REDIS_PORT>,password=<REDIS_PASSWORD>'
DEFAULT_CONNECTION="Server=mssql-svc.default.svc.cluster.local,1433;Database=$DB_NAME;User Id=$DB_USER;Password=$DB_PASSWORD;TrustServerCertificate=True;Encrypt=False;"

# External Services
CLOUDINARY_CLOUDNAME='<CLOUDINARY_CLOUD_NAME>'
CLOUDINARY_APIKEY='<CLOUDINARY_API_KEY>'
CLOUDINARY_APISECRET='<CLOUDINARY_API_SECRET>'

GOOGLE_CLIENT_ID='<GOOGLE_CLIENT_ID>'
GOOGLE_CLIENT_SECRET='<GOOGLE_CLIENT_SECRET>'

GITHUB_CLIENT_ID='<GITHUB_CLIENT_ID>'
GITHUB_CLIENT_SECRET='<GITHUB_CLIENT_SECRET>'

# AI & Analysis
HF_TOKEN='<HUGGINGFACE_API_TOKEN>'
AI_MODERATION_URL='<MODERATION_SERVICE_URL>'

# Email Settings
EMAIL_TOKEN='<EMAIL_API_TOKEN>'
EMAIL_PASSWORD='<EMAIL_SMTP_PASSWORD>'
EMAIL_HOST='<SMTP_HOST>'
EMAIL_PORT='<SMTP_PORT>'
EMAIL_USERNAME='<SMTP_USERNAME>'

# Security
RECAPTCHA_SITEKEY='<RECAPTCHA_SITE_KEY>'
RECAPTCHA_SECRETKEY='<RECAPTCHA_SECRET_KEY>'

# Initial Admin Account
ADMIN_EMAIL='<ADMIN_EMAIL>'
ADMIN_PASSWORD='<ADMIN_PASSWORD>'
ADMIN_FIRST_NAME='System'
ADMIN_LAST_NAME='Admin'

# --- EXECUTION ---
echo "Creating auctionhub-secrets..."

kubectl create secret generic auctionhub-secrets \
  --from-literal=SA_PASSWORD="$SA_PASSWORD" \
  --from-literal=mssql-sa-password="$SA_PASSWORD" \
  --from-literal=DB_USER="$DB_USER" \
  --from-literal=DB_PASSWORD="$DB_PASSWORD" \
  --from-literal=ConnectionStrings__DefaultConnection="$DEFAULT_CONNECTION" \
  --from-literal=redis-connection="$REDIS_CONNECTION" \
  --from-literal=ConnectionStrings__Redis="$REDIS_CONNECTION" \
  --from-literal=cloudinary-cloudname="$CLOUDINARY_CLOUDNAME" \
  --from-literal=cloudinary-apikey="$CLOUDINARY_APIKEY" \
  --from-literal=cloudinary-apisecret="$CLOUDINARY_APISECRET" \
  --from-literal=google-clientid="$GOOGLE_CLIENT_ID" \
  --from-literal=google-clientsecret="$GOOGLE_CLIENT_SECRET" \
  --from-literal=github-clientid="$GITHUB_CLIENT_ID" \
  --from-literal=github-clientsecret="$GITHUB_CLIENT_SECRET" \
  --from-literal=AI__HuggingFaceToken="$HF_TOKEN" \
  --from-literal=AI__ModerationServiceUrl="$AI_MODERATION_URL" \
  --from-literal=email-apitoken="$EMAIL_TOKEN" \
  --from-literal=EmailSettings__Password="$EMAIL_PASSWORD" \
  --from-literal=EmailSettings__Host="$EMAIL_HOST" \
  --from-literal=EmailSettings__Port="$EMAIL_PORT" \
  --from-literal=EmailSettings__Username="$EMAIL_USERNAME" \
  --from-literal=GoogleReCaptcha__SiteKey="$RECAPTCHA_SITEKEY" \
  --from-literal=GoogleReCaptcha__SecretKey="$RECAPTCHA_SECRETKEY" \
  --from-literal=ADMIN_EMAIL="$ADMIN_EMAIL" \
  --from-literal=ADMIN_PASSWORD="$ADMIN_PASSWORD" \
  --from-literal=ADMIN_FIRST_NAME="$ADMIN_FIRST_NAME" \
  --from-literal=ADMIN_LAST_NAME="$ADMIN_LAST_NAME"

echo "Secrets created successfully!"

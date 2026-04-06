#!/bin/bash
# ============================================
# Deploy to AWS ECR — Expense Tracker API
# ============================================
# Usage: chmod +x deploy-ecr.sh && ./deploy-ecr.sh
# ============================================

set -e

ECR_REGISTRY="908027401522.dkr.ecr.us-east-1.amazonaws.com"
ECR_REPO="expense_tracker/api"
IMAGE_TAG="latest"
FULL_IMAGE="${ECR_REGISTRY}/${ECR_REPO}:${IMAGE_TAG}"
AWS_REGION="us-east-1"

echo "========================================"
echo " Deploying to AWS ECR"
echo "========================================"

# 1. Login to ECR
echo ""
echo "[1/4] Logging in to ECR..."
aws ecr get-login-password --region ${AWS_REGION} | docker login --username AWS --password-stdin ${ECR_REGISTRY}

# 2. Build
echo ""
echo "[2/4] Building Docker image..."
docker build -t "${ECR_REPO}:${IMAGE_TAG}" .

# 3. Tag
echo ""
echo "[3/4] Tagging image..."
docker tag "${ECR_REPO}:${IMAGE_TAG}" ${FULL_IMAGE}

# 4. Push
echo ""
echo "[4/4] Pushing to ECR..."
docker push ${FULL_IMAGE}

echo ""
echo "========================================"
echo " Deployed: ${FULL_IMAGE}"
echo "========================================"

# Chạy script này trong PowerShell tại thư mục project:
#   cd D:\PROJECT\AdOpsAgenReviewBanner
#   .\push-to-github.ps1
#
# Yêu cầu: đã cài Git, đã đăng nhập GitHub (gh auth login hoặc SSH key / PAT)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$remoteUrl = "https://github.com/xtechvn/AdOpsAgenReviewBanner.git"

if (-not (Test-Path ".git")) {
    Write-Host "Khoi tao git repo..."
    git init
    git branch -M main
}

$remotes = git remote 2>$null
if ($remotes -notcontains "origin") {
    git remote add origin $remoteUrl
} else {
    git remote set-url origin $remoteUrl
}

Write-Host "Them file (bo qua secret trong .gitignore)..."
git add .
git status -sb

git diff --cached --quiet
$hasChanges = $LASTEXITCODE -ne 0
if ($hasChanges) {
    git commit -m @"
Initial AdOps Agent Review Banner.

- Gemini banner review (Blocked/Reviewed)
- RabbitMQ consumer + Selenium (TEST/Production)
- MongoDB document model
"@
} else {
    Write-Host "Khong co thay doi moi de commit."
}

Write-Host "Push len GitHub..."
git push -u origin main

Write-Host "Xong. Kiem tra: $remoteUrl"

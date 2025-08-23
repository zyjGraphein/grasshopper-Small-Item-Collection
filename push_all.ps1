# --- 自动 Git 推送脚本 (PowerShell) ---

# 1. 获取当前日期和时间作为提交信息
# 格式: yyyy-MM-dd-HH-mm (例如: 2025-08-23-01-32)
$commitMessage = Get-Date -Format "yyyy-MM-dd-HH-mm"

# 2. 执行 Git 命令
Write-Host "Step 1: Staging all changes..."
git add .

Write-Host "Step 2: Committing with message: '$commitMessage'..."
git commit -m $commitMessage

Write-Host "Step 3: Pushing to remote repository..."
git push

# 3. 完成提示
Write-Host ""
Write-Host "✅ All changes have been successfully pushed."

# 暂停脚本，以便用户可以看到输出信息
Read-Host -Prompt "Press Enter to exit"
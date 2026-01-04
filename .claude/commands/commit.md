---
allowed-tools: Bash(git status:*), Bash(git diff:*), Bash(git add:*), Bash(git commit:*), Bash(git push:*)
argument-hint: [commit message]
description: Commit and push changes to GitHub
---

Commit current changes with the provided message:

1. Run `git status` to see what's changed
2. Run `git add -A` to stage all changes
3. Commit with message: "$ARGUMENTS"
4. Push to GitHub

Format the commit message properly with the Claude Code attribution footer.

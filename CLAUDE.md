# Working in this repository

- **Every branch has its own pull request** (open it as soon as the branch is pushed). Branches are named after what they change
  (`feature/...`, `fix/...`). Nothing is pushed straight to `main`.
- **`main` is production** (https://dev-spirit.com - see `docs/DEPLOY.md`). Merge a PR only when its checks and the full test suite
  (`cd sim && dotnet test`) are green. Every other branch and PR gets a **Vercel preview** link, commented on the PR by the Vercel bot.
- Every branch adds an entry to `CHANGELOG.md`.
- Never commit secrets, tokens or `.env` files.

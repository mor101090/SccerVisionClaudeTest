# Deploying the site (Vercel)

The playable page (the match engine compiled to WebAssembly, plus the web page) is a static site. Vercel builds it from this
repository with `vercel.json` -> `tools/demo-page/vercel-build.sh` (it installs the .NET 8 SDK, which Vercel's build machine lacks,
builds the page and the engine, and writes the finished site to `public/`). The first build takes a few minutes.

| Branch | Vercel environment | Address |
|---|---|---|
| `main` | **Production** | the custom domain (see below) |
| every other branch / pull request | **Preview** | its own `*.vercel.app` address, commented on the PR by the Vercel bot |

## One-time setup (a person with access to the Vercel team and to the GitHub organization)
1. **Vercel -> Add New -> Project -> import `SoccerVision/soccer-vision`.** (If the repository is not listed, install the Vercel GitHub
   app on the SoccerVision organization first: GitHub -> Organization settings -> GitHub Apps -> Vercel -> Configure -> add the
   repository.) Leave Framework Preset at *Other*; the build settings come from `vercel.json`. Deploy.
2. **Settings -> Git:** Production Branch = `main` (the default). Pull Request comments stay on.
3. **Settings -> Domains:** add the domain (below). Production = `main`; previews keep their `*.vercel.app` addresses.
4. Previews of a private repository are only visible to people logged in to the Vercel team (Settings -> Deployment Protection).
   Share a preview with someone outside the team through *Share* on the deployment.

## DNS at GoDaddy
`dev-spirit.com` is **already served by Vercel** today (another project: it redirects to `/en`), and so is
`soccer-api.dev-spirit.com`. Pick one:

**A. The game on its own subdomain (nothing existing is touched).** In GoDaddy -> My Products -> `dev-spirit.com` -> DNS -> Add:

| Type | Name | Value | TTL |
|---|---|---|---|
| `CNAME` | `soccer` (gives `soccer.dev-spirit.com`; any name will do) | `cname.vercel-dns.com` - or the value Vercel shows next to the domain under Settings -> Domains (newer projects get a project-specific `xxxx.vercel-dns-017.com`; both work) | 1 hour |

**B. The game on `dev-spirit.com` itself** - this *replaces* the site that is there now. The apex is already correct (`A @ 216.198.79.1`
and `CNAME www -> dev-spirit.com`; `76.76.21.21` is Vercel's older address and also works), so **no DNS change** is needed - but the
domain has to be removed from the current project in Vercel first and added to this one.

If the domain belongs to a *different* Vercel account than this project, Vercel asks for a verification record first:
`TXT` name `_vercel`, value as shown by Vercel (Settings -> Domains -> the domain -> *Verify*). Never leave old `A` / `AAAA`
records for the same name next to the Vercel ones.

## Working with it
Push a branch -> open its pull request -> a minute or two later (a few minutes for the first build) the Vercel bot comments the
preview link. Merge into `main` -> production is rebuilt. Roll back: Vercel -> Deployments -> an older production deployment -> *Promote*.

The GitHub Actions workflow `.github/workflows/publish-demo.yml` (publishes to the `demo` branch for raw.githack / GitHub Pages)
is from when the repository was public; with Vercel it is no longer needed and can be switched off.

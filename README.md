# Jaipur Business Hub

Jaipur-focused marketplace source project for local businesses, suppliers, manufacturers, service providers and buyers.

## Repository layout

- `web/`: Next.js 15, React 19, TypeScript, Tailwind CSS, Redux Toolkit and TanStack Query.
- `src/JaipurHub.Api/`: .NET 8 API, EF Core SQL Server context, JWT access/refresh sessions and REST endpoints.
- `database/schema.sql`: SQL Server schema, indexes, constraints, roles and Jaipur city record.
- `Dockerfile.api`, `Dockerfile.web`, `docker-compose.yml`: local container stack.
- `azure/main.bicep`: Azure App Service + Azure SQL resource deployment template.
- `.github/workflows/jaipur-hub.yml`: build/publish CI workflow.

## Run locally

Requirements: Docker Desktop with Compose, internet access for package/container restore, and a unique secret. From this directory:

1. Copy `.env.example` to `.env`.
2. Set MSSQL_SA_PASSWORD to a strong SQL Server-compliant development password, JWT_SIGNING_KEY to a cryptographically random value with at least 32 UTF-8 bytes, and ADMIN_BOOTSTRAP_SECRET to a unique high-entropy one-time value.
3. Run `docker compose up --build`.
4. Open the web app at http://localhost:3000 and Swagger at http://localhost:5080/swagger.

The compose stack starts SQL Server, applies database/schema.sql, then starts the API and web app. Provision the first administrator once by POSTing fullName, email, phone, password and bootstrapSecret to /api/v1/auth/bootstrap-admin; then clear ADMIN_BOOTSTRAP_SECRET in .env and restart the API. For a local API without Docker, set ConnectionStrings__JaipurHub and Jwt__SigningKey, run the SQL script against your SQL Server, then run `dotnet run --project src/JaipurHub.Api`. For frontend-only development, run `npm install` and `npm run dev` inside web and set NEXT_PUBLIC_API_URL.

## Implemented API surface

- Auth: password registration, login, refresh rotation, logout; hashed passwords and hashed refresh tokens.
- Businesses: public search/profile, authenticated business creation, admin verification.
- Catalog: public product/service listing search, authenticated listing creation.
- Buyers: create requirements and list own requirements.
- Suppliers: read open requirements and submit quotations.
- Buyers: compare quotes on own requirements, save businesses, submit reviews.
- Admin: overview, business queue, lead list and review moderation.
- Health endpoint and Swagger OpenAPI.

## Scope and integration status

The submitted brief requested a complete enterprise marketplace, while production integrations depend on account-specific credentials, domain verification, operational rules and security review. This codebase is a functional foundation, not a claim of production readiness. The pinned .NET 8 line reaches end of support on November 10, 2026; production should upgrade to the supported .NET 10 LTS line after compatibility work. It does not yet implement OTP delivery, Google OIDC, Razorpay order/subscription/webhook handling, email/SMS/WhatsApp dispatch, internal-message APIs, media upload/Blob lifecycle, Elasticsearch indexing, full RFQ negotiation, transaction-verified reviews, paid-plan enforcement, analytics/reports, or complete role dashboards. The subscription cards communicate the requested plan matrix but do not charge customers. Do not enable public sign-up with an admin role; administer roles through an audited provisioning flow before launch.

The code intentionally does not include fake business records or demo credentials. The database starts with roles and Jaipur as a city only. A new account has no published business until it is explicitly created and reviewed. Verification is an admin action; a GST number entered by a user is not independently verified by this foundation.

## Before an external launch

- Replace development SQL settings with managed identity/Key Vault and private endpoints; configure TLS, backups, retention, restore tests and Azure SQL firewall/network policy.
- Add Azure Blob private containers, signed uploads, malware scanning, quotas and retention.
- Configure OTP, OIDC, Razorpay, SMS/WhatsApp and email from provider-approved accounts; verify callback signatures and idempotency.
- Add durable notification and lead-matching workers, rate limits by identity/IP, abuse reporting and moderation workflows.
- Move refresh tokens to secure HttpOnly cookies through a BFF; implement CSRF protection and session revocation UX.
- Add policy-based authorization for business membership, prevent cross-tenant access, and audit all admin changes.
- Add Elasticsearch/OpenSearch and outbox-driven indexing, privacy controls, reviews eligibility and subscription entitlements.
- Add formal CQRS/MediatR application layer, repository abstractions, integration tests, accessibility checks, load tests, alerting and incident runbooks.
- Configure production domains, CORS allowlists, dynamic metadata/sitemap/schema, privacy/terms, local business verification operations and support contacts.

## Azure deployment

Build/publish and push API and web images to a private Azure Container Registry. Create a resource group and deploy `azure/main.bicep` with unique names and secrets passed through a protected pipeline parameter file/Key Vault. Apply `database/schema.sql` through a controlled migration job, not a developer workstation. Configure App Service custom containers to use managed registry identity, private Azure SQL connectivity, HTTPS-only, health checks, CORS to the deployed web origin, Application Insights and Key Vault references. The provided Bicep is a starting deployment template; complete networking, ACR, managed identity, private DNS, monitoring alerts, backups, slots and domain configuration for the target subscription before production.

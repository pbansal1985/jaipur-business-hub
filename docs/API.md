# API reference

Base URL: /api/v1. Swagger is available at /swagger outside Production. Authenticated routes use the JWT access token in the Authorization header. List responses currently use a 20/50/100 item bounded page rather than cursor pagination. Error responses include validation details; the global exception handler returns ProblemDetails-like error envelopes and a trace ID.

## Authentication

- POST /auth/register — {fullName,email,phone,password,role}; roles accepted: Buyer, Supplier, ServiceProvider, Manufacturer.
- POST /auth/login — {email,password}; returns access and refresh tokens.
- POST /auth/refresh — {refreshToken}; revokes the old refresh token and returns a rotated pair.
- POST /auth/logout — {refreshToken}; revoke refresh session.
- POST /auth/bootstrap-admin — one-time setup; requires {fullName,email,phone,password,bootstrapSecret}. Configure Admin__BootstrapSecret as a high-entropy secret, call this endpoint once, then remove the secret and restart. Existing Admin prevents further bootstrap.

## Businesses and catalog

- GET /businesses?q=&area=&page= — public published businesses.
- GET /businesses/{slug} — public published profile.
- POST /businesses — authenticated owner creates an unpublished business.
- GET /businesses/{id}/listings — public active listings of published business.
- GET /listings?kind=Product|Service&q=&category=&page= — public catalog search.
- POST /listings — authenticated supplier business owner creates Product or Service listing.
- POST /businesses/{id}/save — save a published business.

## Requirements, quotes and reviews

- POST /leads — authenticated buyer creates requirement {title,category,quantity,unit,city,area,description}.
- GET /leads — current buyer's requirements.
- GET /leads/open?category=&area= — authenticated marketplace leads.
- POST /leads/{id}/quotes — supplier business submits {amount,terms,validDays}.
- GET /leads/{id}/quotes — owning buyer compares quotations.
- POST /businesses/{id}/reviews — submit {rating,body}; review stays Pending until admin moderation.

## Admin

All routes require Admin role:
- GET /admin/overview
- GET /admin/businesses?page=1
- PATCH /admin/businesses/{id}/verification?verified=true|false
- PATCH /admin/businesses/{id}/publication?published=true|false
- GET /admin/leads
- PATCH /admin/reviews/{id}/moderation?status=Approved|Rejected

## Current contract boundaries

The REST endpoints cover the working foundation and are not an implementation of every module in the brief. OTP/Google, Razorpay, messaging, notifications, media, Elasticsearch, RFQ negotiation, payment webhooks, plan entitlements and analytics endpoints remain future work. OpenAPI is served from Swagger in non-production; publish an OpenAPI document to CI artifacts for release review.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JaipurHub.Api.Contracts;
using JaipurHub.Api.Domain;
using JaipurHub.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("JaipurHub") ?? throw new InvalidOperationException("ConnectionStrings:JaipurHub is required.");
var signingKey = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
if (Encoding.UTF8.GetByteCount(signingKey) < 32) throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");
builder.Services.AddDbContext<HubDbContext>(o=>o.UseSqlServer(connection));
builder.Services.AddScoped<IPasswordHasher<User>,PasswordHasher<User>>();
builder.Services.AddCors(o=>o.AddPolicy("web",p=>p.WithOrigins(builder.Configuration.GetSection("Web:Origins").Get<string[]>() ?? ["http://localhost:3000"]).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(o=>o.AddFixedWindowLimiter("auth",x=>{x.Window=TimeSpan.FromMinutes(1);x.PermitLimit=8;x.QueueLimit=0;}));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>{
 o.TokenValidationParameters=new TokenValidationParameters{ValidateIssuer=true,ValidIssuer=builder.Configuration["Jwt:Issuer"]??"JaipurBusinessHub",ValidateAudience=true,ValidAudience=builder.Configuration["Jwt:Audience"]??"JaipurBusinessHub.Web",ValidateIssuerSigningKey=true,IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),ValidateLifetime=true,ClockSkew=TimeSpan.FromSeconds(30)};
});
builder.Services.AddAuthorization(o=>o.AddPolicy("Admin",p=>p.RequireRole("Admin")));
builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();
var app=builder.Build();
app.UseExceptionHandler(handler=>handler.Run(async ctx=>{ctx.Response.StatusCode=500;ctx.Response.ContentType="application/json";await ctx.Response.WriteAsJsonAsync(new ApiError("server_error","The request could not be completed.",ctx.TraceIdentifier));}));
app.UseCors("web"); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
if(!app.Environment.IsProduction()){app.UseSwagger(); app.UseSwaggerUI();}
app.MapGet("/health",()=>Results.Ok(new{status="ok"}));

var auth=app.MapGroup("/api/v1/auth");
auth.MapPost("/bootstrap-admin",async Task<IResult> (BootstrapAdminRequest r,HubDbContext db,IPasswordHasher<User> hasher,IConfiguration cfg,CancellationToken ct)=>{
 var expected=cfg["Admin:BootstrapSecret"];if(string.IsNullOrWhiteSpace(expected)||!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected),Encoding.UTF8.GetBytes(r.BootstrapSecret)))return Results.Unauthorized();
 if(await db.Users.AnyAsync(x=>x.Role=="Admin",ct))return Results.Conflict(new ApiError("admin_exists","An administrator has already been provisioned.",string.Empty));
 if(r.Password.Length<16||!r.Email.Contains("@"))return Results.ValidationProblem(new Dictionary<string,string[]>{{"credentials",["Use a valid email and a password of at least 16 characters."]}});
 var u=new User{Email=r.Email.Trim().ToLowerInvariant(),Phone=r.Phone.Trim(),FullName=r.FullName.Trim(),Role="Admin"};u.PasswordHash=hasher.HashPassword(u,r.Password);db.Users.Add(u);db.AuditLogs.Add(new AuditLog{ActorId=u.Id,Action="admin.bootstrap",Entity="User"});await db.SaveChangesAsync(ct);return Results.Ok(await Issue(u,db,signingKey,ct));
}).RequireRateLimiting("auth");
auth.MapPost("/register",async Task<IResult> (RegisterRequest r,HubDbContext db,IPasswordHasher<User> hasher,CancellationToken ct)=>{
 var email=r.Email.Trim().ToLowerInvariant(); if(string.IsNullOrWhiteSpace(r.FullName)||!email.Contains('@')||r.Password.Length<12||r.Phone.Length<8)return Results.ValidationProblem(new Dictionary<string,string[]>{{"registration",["Enter a name, valid email, phone, and password of at least 12 characters."]}});
 if(r.Role is not ("Buyer" or "Supplier" or "ServiceProvider" or "Manufacturer"))return Results.ValidationProblem(new Dictionary<string,string[]>{{"role",["Choose a supported account type."]}});
 if(await db.Users.AnyAsync(x=>x.Email==email||x.Phone==r.Phone,ct))return Results.Conflict(new ApiError("account_exists","Email or phone is already registered.", ""));
 var u=new User{Email=email,Phone=r.Phone.Trim(),FullName=r.FullName.Trim(),Role=r.Role};u.PasswordHash=hasher.HashPassword(u,r.Password);db.Users.Add(u);db.AuditLogs.Add(new AuditLog{ActorId=u.Id,Action="user.register",Entity="User"});await db.SaveChangesAsync(ct);return Results.Ok(await Issue(u,db,signingKey,ct));
}).RequireRateLimiting("auth");
auth.MapPost("/login",async Task<IResult> (LoginRequest r,HubDbContext db,IPasswordHasher<User> hasher,CancellationToken ct)=>{
 var u=await db.Users.SingleOrDefaultAsync(x=>x.Email==r.Email.Trim().ToLowerInvariant()&&x.IsActive,ct);if(u is null||hasher.VerifyHashedPassword(u,u.PasswordHash,r.Password)==PasswordVerificationResult.Failed)return Results.Unauthorized();return Results.Ok(await Issue(u,db,signingKey,ct));
}).RequireRateLimiting("auth");
auth.MapPost("/refresh",async Task<IResult> (RefreshBody r,HubDbContext db,CancellationToken ct)=>{
 var hash=Hash(r.RefreshToken);var s=await db.RefreshSessions.SingleOrDefaultAsync(x=>x.TokenHash==hash&&x.RevokedAt==null&&x.ExpiresAt>DateTime.UtcNow,ct);if(s is null)return Results.Unauthorized();
 s.RevokedAt=DateTime.UtcNow;var u=await db.Users.SingleAsync(x=>x.Id==s.UserId,ct);return Results.Ok(await Issue(u,db,signingKey,ct));
}).RequireRateLimiting("auth");
auth.MapPost("/logout",async Task<IResult> (RefreshBody r,HubDbContext db,CancellationToken ct)=>{var s=await db.RefreshSessions.SingleOrDefaultAsync(x=>x.TokenHash==Hash(r.RefreshToken)&&x.RevokedAt==null,ct);if(s is not null){s.RevokedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);}return Results.NoContent();});

var api=app.MapGroup("/api/v1");
api.MapGet("/businesses",async Task<IResult> (string? q,string? category,string? area,int page,HubDbContext db,CancellationToken ct)=>{
 page=Math.Clamp(page,1,1000);var query=db.Businesses.AsNoTracking().Where(x=>x.IsPublished);
 if(!string.IsNullOrWhiteSpace(q))query=query.Where(x=>x.Name.Contains(q)||x.Description.Contains(q)||x.BusinessType.Contains(q));
 if(!string.IsNullOrWhiteSpace(area))query=query.Where(x=>x.Area==area);
 var total=await query.CountAsync(ct);var rows=await query.OrderByDescending(x=>x.IsVerified).ThenBy(x=>x.Name).Skip((page-1)*20).Take(20).Select(x=>new{x.Id,x.Name,x.Slug,x.Description,x.BusinessType,x.City,x.Area,x.IsVerified}).ToListAsync(ct);
 return Results.Ok(new{items=rows,total,page,pageSize=20});
});
api.MapGet("/businesses/{slug}",async Task<IResult> (string slug,HubDbContext db,CancellationToken ct)=>{var b=await db.Businesses.AsNoTracking().Where(x=>x.Slug==slug&&x.IsPublished).Select(x=>new{x.Id,x.Name,x.Slug,x.Description,x.BusinessType,x.Gstin,x.Phone,x.Email,x.City,x.Area,x.Address,x.IsVerified}).SingleOrDefaultAsync(ct);return b is null?Results.NotFound():Results.Ok(b);});
api.MapPost("/businesses",async Task<IResult> (BusinessRequest r,ClaimsPrincipal user,HubDbContext db,CancellationToken ct)=>{
 var owner=Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);var slug=Slug(r.Name);var baseSlug=slug;var i=2;while(await db.Businesses.AnyAsync(x=>x.Slug==slug,ct))slug=baseSlug+"-"+i++;
 var b=new Business{OwnerId=owner,Name=r.Name.Trim(),Slug=slug,Description=r.Description,BusinessType=r.BusinessType,Gstin=r.Gstin,Phone=r.Phone,Email=r.Email,City=string.IsNullOrWhiteSpace(r.City)?"Jaipur":r.City,Area=r.Area,Address=r.Address};db.Businesses.Add(b);await db.SaveChangesAsync(ct);return Results.Created($"/api/v1/businesses/{b.Slug}",new{id=b.Id,b.Slug});
}).RequireAuthorization();
api.MapGet("/businesses/{id:guid}/listings",async Task<IResult> (Guid id,HubDbContext db,CancellationToken ct)=>Results.Ok(await db.Listings.AsNoTracking().Where(x=>x.BusinessId==id&&x.IsActive).OrderBy(x=>x.Name).ToListAsync(ct)));
api.MapPost("/listings",async Task<IResult> (ListingRequest r,ClaimsPrincipal user,HubDbContext db,CancellationToken ct)=>{
 var uid=Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);var b=await db.Businesses.SingleOrDefaultAsync(x=>x.OwnerId==uid,ct);if(b is null)return Results.BadRequest(new ApiError("business_required","Create a business profile before publishing listings.",""));
 if(r.Kind is not ("Product" or "Service"))return Results.ValidationProblem(new Dictionary<string,string[]>{{"kind",["Kind must be Product or Service."]}});
 var l=new Listing{BusinessId=b.Id,Kind=r.Kind,Name=r.Name,Category=r.Category,Description=r.Description,PriceFrom=r.PriceFrom,PriceTo=r.PriceTo,Unit=r.Unit,MinimumOrderQuantity=r.MinimumOrderQuantity};db.Listings.Add(l);await db.SaveChangesAsync(ct);return Results.Created($"/api/v1/listings/{l.Id}",new{id=l.Id});
}).RequireAuthorization();
api.MapGet("/listings",async Task<IResult> (string? kind,string? q,string? category,int page,HubDbContext db,CancellationToken ct)=>{
 var query=db.Listings.AsNoTracking().Where(x=>x.IsActive&&x.Business!.IsPublished);if(kind is not null)query=query.Where(x=>x.Kind==kind);if(category is not null)query=query.Where(x=>x.Category==category);if(q is not null)query=query.Where(x=>x.Name.Contains(q)||x.Description.Contains(q));
 page=Math.Clamp(page,1,1000);var total=await query.CountAsync(ct);var items=await query.OrderByDescending(x=>x.CreatedAt).Skip((page-1)*20).Take(20).Select(x=>new{x.Id,x.Kind,x.Name,x.Category,x.Description,x.PriceFrom,x.PriceTo,x.Unit,x.MinimumOrderQuantity,Business=x.Business!.Name,Area=x.Business.Area,Verified=x.Business.IsVerified}).ToListAsync(ct);return Results.Ok(new{items,total,page,pageSize=20});
});
api.MapPost("/leads",async Task<IResult> (LeadRequest r,ClaimsPrincipal user,HubDbContext db,CancellationToken ct)=>{
 var id=Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);if(r.Quantity<=0||string.IsNullOrWhiteSpace(r.Title))return Results.ValidationProblem(new Dictionary<string,string[]>{{"lead",["Title and positive quantity are required."]}});
 var l=new Lead{BuyerId=id,Title=r.Title,Category=r.Category,Quantity=r.Quantity,Unit=r.Unit,City=r.City,Area=r.Area,Description=r.Description};db.Leads.Add(l);await db.SaveChangesAsync(ct);return Results.Created($"/api/v1/leads/{l.Id}",new{id=l.Id,status=l.Status});
}).RequireAuthorization();
api.MapGet("/leads",async Task<IResult> (ClaimsPrincipal user,HubDbContext db,CancellationToken ct)=>Results.Ok(await db.Leads.AsNoTracking().Where(x=>x.BuyerId==Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!)).OrderByDescending(x=>x.CreatedAt).Take(100).ToListAsync(ct))).RequireAuthorization();
api.MapGet("/leads/open",async Task<IResult> (string? category,string? area,HubDbContext db,CancellationToken ct)=>{var q=db.Leads.AsNoTracking().Where(x=>x.Status=="Open");if(category is not null)q=q.Where(x=>x.Category==category);if(area is not null)q=q.Where(x=>x.Area==area);return Results.Ok(await q.OrderByDescending(x=>x.CreatedAt).Take(50).ToListAsync(ct));}).RequireAuthorization();
api.MapPost("/leads/{id:guid}/quotes",async Task<IResult> (Guid id,QuoteRequest r,ClaimsPrincipal user,HubDbContext db,CancellationToken ct)=>{
 var uid=Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);var b=await db.Businesses.SingleOrDefaultAsync(x=>x.OwnerId==uid,ct);if(b is null)return Results.BadRequest(new ApiError("business_required","Create a supplier business first.",string.Empty));var lead=await db.Leads.SingleOrDefaultAsync(x=>x.Id==id&&x.Status=="Open",ct);if(lead is null)return Results.NotFound();if(r.Amount<=0)return Results.ValidationProblem(new Dictionary<string,string[]>{{"amount",["Quote must be positive."]}});
 var q=new Quotation{LeadId=id,SupplierBusinessId=b.Id,Amount=r.Amount,Terms=r.Terms,ValidDays=Math.Clamp(r.ValidDays,1,90)};db.Quotations.Add(q);await db.SaveChangesAsync(ct);return Results.Created($"/api/v1/quotes/{q.Id}",new{id=q.Id,status=q.Status});
}).RequireAuthorization();
api.MapGet("/leads/{id:guid}/quotes",async Task<IResult> (Guid id,ClaimsPrincipal u,HubDbContext db,CancellationToken ct)=>{var uid=Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);var lead=await db.Leads.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);if(lead is null)return Results.NotFound();if(lead.BuyerId!=uid)return Results.Forbid();return Results.Ok(await db.Quotations.AsNoTracking().Where(x=>x.LeadId==id).OrderBy(x=>x.Amount).ToListAsync(ct));}).RequireAuthorization();
api.MapPost("/businesses/{id:guid}/reviews",async Task<IResult> (Guid id,ReviewRequest r,ClaimsPrincipal u,HubDbContext db,CancellationToken ct)=>{if(r.Rating is <1 or >5)return Results.ValidationProblem(new Dictionary<string,string[]>{{"rating",["Rating must be from 1 to 5."]}});var review=new Review{BusinessId=id,AuthorId=Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!),Rating=r.Rating,Body=r.Body};db.Reviews.Add(review);await db.SaveChangesAsync(ct);return Results.Created($"/api/v1/reviews/{review.Id}",new{id=review.Id,status=review.Status});}).RequireAuthorization();
api.MapPost("/businesses/{id:guid}/save",async Task<IResult> (Guid id,ClaimsPrincipal u,HubDbContext db,CancellationToken ct)=>{var uid=Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);if(!await db.Businesses.AnyAsync(x=>x.Id==id&&x.IsPublished,ct))return Results.NotFound();if(!await db.SavedBusinesses.AnyAsync(x=>x.UserId==uid&&x.BusinessId==id,ct)){db.SavedBusinesses.Add(new SavedBusiness{UserId=uid,BusinessId=id});await db.SaveChangesAsync(ct);}return Results.NoContent();}).RequireAuthorization();
var admin=app.MapGroup("/api/v1/admin").RequireAuthorization("Admin");
admin.MapGet("/overview",async Task<IResult> (HubDbContext db,CancellationToken ct)=>Results.Ok(new{users=await db.Users.CountAsync(ct),businesses=await db.Businesses.CountAsync(ct),activeListings=await db.Listings.CountAsync(x=>x.IsActive,ct),openLeads=await db.Leads.CountAsync(x=>x.Status=="Open",ct),pendingReviews=await db.Reviews.CountAsync(x=>x.Status=="Pending",ct)}));
admin.MapPatch("/businesses/{id:guid}/publication",async Task<IResult> (Guid id,bool published,HubDbContext db,CancellationToken ct)=>{var b=await db.Businesses.FindAsync([id],ct);if(b is null)return Results.NotFound();b.IsPublished=published;await db.SaveChangesAsync(ct);return Results.NoContent();});
admin.MapGet("/businesses",async Task<IResult> (int page,HubDbContext db,CancellationToken ct)=>Results.Ok(await db.Businesses.AsNoTracking().OrderByDescending(x=>x.CreatedAt).Skip(Math.Max(0,page-1)*30).Take(30).ToListAsync(ct)));
admin.MapPatch("/businesses/{id:guid}/verification",async Task<IResult> (Guid id,bool verified,HubDbContext db,CancellationToken ct)=>{var b=await db.Businesses.FindAsync([id],ct);if(b is null)return Results.NotFound();b.IsVerified=verified;await db.SaveChangesAsync(ct);return Results.NoContent();});
admin.MapGet("/leads",async Task<IResult> (HubDbContext db,CancellationToken ct)=>Results.Ok(await db.Leads.AsNoTracking().OrderByDescending(x=>x.CreatedAt).Take(100).ToListAsync(ct)));
admin.MapPatch("/reviews/{id:guid}/moderation",async Task<IResult> (Guid id,string status,HubDbContext db,CancellationToken ct)=>{if(status is not ("Approved" or "Rejected"))return Results.BadRequest();var r=await db.Reviews.FindAsync([id],ct);if(r is null)return Results.NotFound();r.Status=status;await db.SaveChangesAsync(ct);return Results.NoContent();});
app.Run();

static string Slug(string text){var s=System.Text.RegularExpressions.Regex.Replace(text.ToLowerInvariant(),"[^a-z0-9]+","-").Trim('-');return string.IsNullOrWhiteSpace(s)?Guid.NewGuid().ToString("N"):s;}
static string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
static async Task<TokenResponse> Issue(User u,HubDbContext db,string key,CancellationToken ct){
 var now=DateTime.UtcNow;var expires=now.AddMinutes(15);var claims=new[]{new Claim(ClaimTypes.NameIdentifier,u.Id.ToString()),new Claim(ClaimTypes.Name,u.FullName),new Claim(ClaimTypes.Email,u.Email),new Claim(ClaimTypes.Role,u.Role)};
 var creds=new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),SecurityAlgorithms.HmacSha256);
 var jwt=new JwtSecurityToken("JaipurBusinessHub","JaipurBusinessHub.Web",claims,now,expires,creds);var refresh=Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));db.RefreshSessions.Add(new RefreshSession{UserId=u.Id,TokenHash=Hash(refresh),ExpiresAt=now.AddDays(30)});await db.SaveChangesAsync(ct);return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(jwt),refresh,expires);
}
record RefreshBody(string RefreshToken);

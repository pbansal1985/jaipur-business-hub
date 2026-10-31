namespace JaipurHub.Api.Domain;

public sealed class User
{
 public Guid Id {get;set;}=Guid.NewGuid(); public string Email {get;set;}=""; public string Phone {get;set;}="";
 public string FullName {get;set;}=""; public string PasswordHash {get;set;}=""; public string Role {get;set;}="Buyer";
 public bool IsActive {get;set;}=true; public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
}
public sealed class Business
{
 public Guid Id {get;set;}=Guid.NewGuid(); public Guid OwnerId {get;set;} public string Name {get;set;}="";
 public string Slug {get;set;}=""; public string Description {get;set;}=""; public string BusinessType {get;set;}="";
 public string Gstin {get;set;}=""; public string Phone {get;set;}=""; public string Email {get;set;}="";
 public string City {get;set;}="Jaipur"; public string Area {get;set;}=""; public string Address {get;set;}="";
 public bool IsVerified {get;set;} public bool IsPublished {get;set;} public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
}
public sealed class Listing
{
 public Guid Id {get;set;}=Guid.NewGuid(); public Guid BusinessId {get;set;} public Business? Business {get;set;}
 public string Kind {get;set;}="Product"; public string Name {get;set;}=""; public string Category {get;set;}="";
 public string Description {get;set;}=""; public decimal? PriceFrom {get;set;} public decimal? PriceTo {get;set;}
 public string Unit {get;set;}=""; public int? MinimumOrderQuantity {get;set;} public bool IsActive {get;set;}=true;
 public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
}
public sealed class Lead
{
 public Guid Id {get;set;}=Guid.NewGuid(); public Guid BuyerId {get;set;} public string Title {get;set;}="";
 public string Category {get;set;}=""; public decimal Quantity {get;set;} public string Unit {get;set;}="";
 public string City {get;set;}="Jaipur"; public string Area {get;set;}=""; public string Description {get;set;}="";
 public string Status {get;set;}="Open"; public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
}
public sealed class Quotation
{
 public Guid Id {get;set;}=Guid.NewGuid(); public Guid LeadId {get;set;} public Guid SupplierBusinessId {get;set;}
 public decimal Amount {get;set;} public string Currency {get;set;}="INR"; public string Terms {get;set;}="";
 public int ValidDays {get;set;}=7; public string Status {get;set;}="Submitted"; public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
}
public sealed class Review
{
 public Guid Id {get;set;}=Guid.NewGuid(); public Guid BusinessId {get;set;} public Guid AuthorId {get;set;}
 public int Rating {get;set;} public string Body {get;set;}=""; public string Status {get;set;}="Pending";
 public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
}
public sealed class SavedBusiness { public Guid UserId {get;set;} public Guid BusinessId {get;set;} public DateTime CreatedAt {get;set;}=DateTime.UtcNow; }
public sealed class RefreshSession { public Guid Id {get;set;}=Guid.NewGuid(); public Guid UserId {get;set;} public string TokenHash {get;set;}=""; public DateTime ExpiresAt {get;set;} public DateTime? RevokedAt {get;set;} }
public sealed class AuditLog { public long Id {get;set;} public Guid? ActorId {get;set;} public string Action {get;set;}=""; public string Entity {get;set;}=""; public string Ip {get;set;}=""; public DateTime CreatedAt {get;set;}=DateTime.UtcNow; }
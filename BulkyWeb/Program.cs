using Bulky.DataAccess.Repository;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.DataAcess.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Bulky.Utility;
using Microsoft.AspNetCore.Identity.UI.Services;
using Stripe;
using Bulky.DataAccess.DbInitializer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options => 
                                                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
//section defined in app settings -Stripe
//get values and inject them into the properties defined in stripe class
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));

builder.Services.AddIdentity<IdentityUser,IdentityRole >().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
//add this only after identity service
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = $"/Identity/Account/Login";
    options.LogoutPath = $"/Identity/Account/Logout";
    options.AccessDeniedPath = $"/Identity/Account/AccessDenied";
});

//configure facebook authentication
builder.Services.AddAuthentication().AddFacebook(option =>
{
    option.AppId = "374634385345875";
    option.AppSecret = "f390422e9229d08602ab5f3e482c7092";
});

//configure session and its required properties
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(100);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// the database seeder service
builder.Services.AddScoped<IDbInitializer, DbInitializer>();

//add razor pages as identity functionality is built using razor pages
builder.Services.AddRazorPages();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IEmailSender, EmailSender>();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
StripeConfiguration.ApiKey = builder.Configuration.GetSection("Stripe:SecretKey").Get<string>();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

//use session in the request pipeline
app.UseSession();

SeedDatabase();
//routing to map and use the identity function which is designed using razor pages
app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{area=Customer}/{controller=Home}/{action=Index}/{id?}");

app.Run();

//database initialization and seeding on deployment
void SeedDatabase()
{ 
    //create a service scope and add the interface to the service using a service provider
    using (var scope = app.Services.CreateScope())
    {
      var dbInitializer =  scope.ServiceProvider.GetRequiredService<IDbInitializer>();
        //initialize the service 
        dbInitializer.Initialize();
    }

}

using LDFRecruitment.Data;
using LDFRecruitment.Models;
using LDFRecruitment.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Ephemeral keys: every app restart invalidates old login cookies, forcing re-login.
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddTransient<IEmailSender, EmailSender>();

builder.Services.AddControllersWithViews();
builder.Services.AddSession();
builder.Services.AddScoped<RuleBasedEligibilityService>();
builder.Services.AddScoped<CertificateExtractionService>();

builder.Services.AddHttpClient("MLService", client =>
{
    client.BaseAddress = new Uri("http://localhost:8001/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

    string[] roleNames = { "Applicant", "RecruitmentOfficer" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    // Seed one test recruitment officer account
    string officerEmail = "officer@ldf.test";
    string officerPassword = "Officer@1234";

    if (await userManager.FindByEmailAsync(officerEmail) == null)
    {
        var officerUser = new IdentityUser
        {
            UserName = officerEmail,
            Email = officerEmail,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(officerUser, officerPassword);
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(officerUser, "RecruitmentOfficer");
        }
    }

    // Seed sample test questions (only if none exist yet)
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (!dbContext.Questions.Any())
    {
        dbContext.Questions.AddRange(
            // Mathematics
            new Question { Category = "Mathematics", Text = "Solve for x: 3x + 7 = 22", OptionA = "x = 5", OptionB = "x = 6", OptionC = "x = 7", OptionD = "x = 8", CorrectOption = "A" },
            new Question { Category = "Mathematics", Text = "What is the value of x in the equation x^2 = 81?", OptionA = "±7", OptionB = "±8", OptionC = "±9", OptionD = "±10", CorrectOption = "C" },
            new Question { Category = "Mathematics", Text = "A triangle has angles of 50° and 60°. What is the third angle?", OptionA = "60°", OptionB = "70°", OptionC = "80°", OptionD = "90°", CorrectOption = "B" },
            new Question { Category = "Mathematics", Text = "What is 15% of 240?", OptionA = "24", OptionB = "30", OptionC = "36", OptionD = "40", CorrectOption = "C" },

            // Science
            new Question { Category = "Science", Text = "Which gas do plants absorb from the atmosphere during photosynthesis?", OptionA = "Oxygen", OptionB = "Nitrogen", OptionC = "Carbon dioxide", OptionD = "Hydrogen", CorrectOption = "C" },
            new Question { Category = "Science", Text = "What is the chemical symbol for Sodium?", OptionA = "So", OptionB = "Sd", OptionC = "S", OptionD = "Na", CorrectOption = "D" },
            new Question { Category = "Science", Text = "Which part of the human body is primarily responsible for pumping blood?", OptionA = "Lungs", OptionB = "Heart", OptionC = "Liver", OptionD = "Kidney", CorrectOption = "B" },
            new Question { Category = "Science", Text = "What force pulls objects toward the centre of the Earth?", OptionA = "Magnetism", OptionB = "Friction", OptionC = "Gravity", OptionD = "Tension", CorrectOption = "C" },

            // English
            new Question { Category = "English", Text = "Identify the correctly punctuated sentence.", OptionA = "Its a hot day, isnt it?", OptionB = "It's a hot day, isn't it?", OptionC = "Its' a hot day, isn't it?", OptionD = "It's a hot day, isnt' it?", CorrectOption = "B" },
            new Question { Category = "English", Text = "Choose the word that is a synonym for 'diligent'.", OptionA = "Lazy", OptionB = "Hardworking", OptionC = "Careless", OptionD = "Slow", CorrectOption = "B" },
            new Question { Category = "English", Text = "Which sentence uses the past perfect tense correctly?", OptionA = "She had finished her homework before dinner.", OptionB = "She has finished her homework before dinner.", OptionC = "She finish her homework before dinner.", OptionD = "She finishing her homework before dinner.", CorrectOption = "A" },

            // ICT
            new Question { Category = "ICT", Text = "What does 'RAM' stand for in computing?", OptionA = "Random Access Memory", OptionB = "Read Access Memory", OptionC = "Rapid Access Module", OptionD = "Read And Modify", CorrectOption = "A" },
            new Question { Category = "ICT", Text = "Which of the following is an example of an operating system?", OptionA = "Microsoft Word", OptionB = "Windows", OptionC = "Google Chrome", OptionD = "Adobe Photoshop", CorrectOption = "B" },
            new Question { Category = "ICT", Text = "What is the main function of a firewall in a computer network?", OptionA = "To speed up internet connection", OptionB = "To store large files", OptionC = "To block unauthorized access", OptionD = "To print documents", CorrectOption = "C" },
            new Question { Category = "ICT", Text = "A file with the extension '.xlsx' is most likely which type of file?", OptionA = "A spreadsheet", OptionB = "A video", OptionC = "A presentation", OptionD = "An image", CorrectOption = "A" },

            // Sesotho
            new Question { Category = "Sesotho", Text = "Ke lentsoe lefe le hlalosang 'motho ea etsang mosebetsi ka thata'?", OptionA = "Letsoalloa", OptionB = "Sebetsi", OptionC = "Mosebetsi o motle", OptionD = "Sebete", CorrectOption = "B" },
            new Question { Category = "Sesotho", Text = "'Ntate' ho Sesotho e bolela eng ka Senyesemane?", OptionA = "Mother", OptionB = "Sister", OptionC = "Father", OptionD = "Brother", CorrectOption = "C" },
            new Question { Category = "Sesotho", Text = "Ho re 'Khotso, Pula, Nala' ke mantsoe afe a naha ea Lesotho?", OptionA = "Sefapano sa naha", OptionB = "Seala sa naha", OptionC = "Lets'oao la sesole", OptionD = "Pina ea naha feela", CorrectOption = "B" },

            // General Knowledge
            new Question { Category = "General Knowledge", Text = "What is the capital city of Lesotho?", OptionA = "Maseru", OptionB = "Leribe", OptionC = "Mafeteng", OptionD = "Roma", CorrectOption = "A" },
            new Question { Category = "General Knowledge", Text = "Lesotho is completely surrounded by which country?", OptionA = "Botswana", OptionB = "Namibia", OptionC = "South Africa", OptionD = "Zimbabwe", CorrectOption = "C" },
            new Question { Category = "General Knowledge", Text = "Who is generally regarded as the founder of the Basotho nation?", OptionA = "Moshoeshoe I", OptionB = "Letsie III", OptionC = "Mokhehle", OptionD = "Jonathan", CorrectOption = "A" },
            new Question { Category = "General Knowledge", Text = "Lesotho is often referred to by which nickname, relating to its geography?", OptionA = "The Green Island", OptionB = "The Kingdom in the Sky", OptionC = "The Desert Kingdom", OptionD = "The Golden Land", CorrectOption = "B" }
        );
        await dbContext.SaveChangesAsync();
    }
}

app.Run();
using LDFRecruitment.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LDFRecruitment.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider sp, IConfiguration config)
    {
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<IdentityUser>>();
        var db = sp.GetRequiredService<ApplicationDbContext>();

        foreach (var role in new[] { Roles.Applicant, Roles.Officer, Roles.Administrator })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        // Demo accounts for the evaluation environment. Change these passwords in appsettings / user-secrets.
        await EnsureUserAsync(userManager, config["SeedAccounts:OfficerEmail"], config["SeedAccounts:OfficerPassword"], Roles.Officer);
        await EnsureUserAsync(userManager, config["SeedAccounts:AdminEmail"], config["SeedAccounts:AdminPassword"], Roles.Administrator);

        if (!await db.Questions.AnyAsync())
        {
            db.Questions.AddRange(Questions());
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureUserAsync(UserManager<IdentityUser> um, string? email, string? password, string role)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        if (await um.FindByEmailAsync(email) != null) return;
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var created = await um.CreateAsync(user, password);
        if (created.Succeeded) await um.AddToRoleAsync(user, role);
    }

    private static Question Q(string cat, string text, string a, string b, string c, string d, string correct) =>
        new() { Category = cat, Text = text, OptionA = a, OptionB = b, OptionC = c, OptionD = d, CorrectOption = correct };

    // LGCSE-level sample bank: replace/extend through the admin Questions page and have it reviewed (Methodology 3.7.5).
    private static IEnumerable<Question> Questions() => new[]
    {
        Q("Mathematics", "Solve for x: 3x + 7 = 22", "x = 5", "x = 6", "x = 7", "x = 8", "A"),
        Q("Mathematics", "What is the positive value of x if x squared = 81?", "7", "8", "9", "10", "C"),
        Q("Mathematics", "A triangle has angles of 50 and 60 degrees. What is the third angle?", "60 degrees", "70 degrees", "80 degrees", "90 degrees", "B"),
        Q("Mathematics", "What is 15% of 240?", "24", "30", "36", "40", "C"),

        Q("Science", "Which gas do plants absorb from the atmosphere during photosynthesis?", "Oxygen", "Nitrogen", "Carbon dioxide", "Hydrogen", "C"),
        Q("Science", "What is the chemical symbol for sodium?", "So", "Sd", "S", "Na", "D"),
        Q("Science", "Which organ is primarily responsible for pumping blood?", "Lungs", "Heart", "Liver", "Kidney", "B"),
        Q("Science", "Which force pulls objects towards the centre of the Earth?", "Magnetism", "Friction", "Gravity", "Tension", "C"),

        Q("English", "Identify the correctly punctuated sentence.", "Its a hot day, isnt it?", "It's a hot day, isn't it?", "Its' a hot day, isn't it?", "It's a hot day, isnt' it?", "B"),
        Q("English", "Choose the word that is a synonym for 'diligent'.", "Lazy", "Hardworking", "Careless", "Slow", "B"),
        Q("English", "Which sentence uses the past perfect tense correctly?", "She had finished her homework before dinner.", "She has finished her homework before dinner.", "She finish her homework before dinner.", "She finishing her homework before dinner.", "A"),

        Q("ICT", "What does RAM stand for in computing?", "Random Access Memory", "Read Access Memory", "Rapid Access Module", "Read And Modify", "A"),
        Q("ICT", "Which of the following is an operating system?", "Microsoft Word", "Windows", "Google Chrome", "Adobe Photoshop", "B"),
        Q("ICT", "What is the main function of a firewall in a network?", "Speed up the connection", "Store large files", "Block unauthorised access", "Print documents", "C"),
        Q("ICT", "A file with the extension .xlsx is most likely which type of file?", "A spreadsheet", "A video", "A presentation", "An image", "A"),

        Q("Sesotho", "'Ntate' ho Sesotho e bolela eng ka Senyesemane?", "Mother", "Sister", "Father", "Brother", "C"),
        Q("Sesotho", "Lentsoe 'mosebetsi' le bolela eng?", "Work", "Food", "Water", "Road", "A"),
        Q("Sesotho", "Lentsoe 'khotso' le bolela eng?", "War", "Peace", "Rain", "Mountain", "B"),

        Q("General Knowledge", "What is the capital city of Lesotho?", "Maseru", "Leribe", "Mafeteng", "Roma", "A"),
        Q("General Knowledge", "Lesotho is completely surrounded by which country?", "Botswana", "Namibia", "South Africa", "Zimbabwe", "C"),
        Q("General Knowledge", "Who is regarded as the founder of the Basotho nation?", "Moshoeshoe I", "Letsie III", "Mokhehle", "Jonathan", "A"),
        Q("General Knowledge", "Lesotho is often called which of the following?", "The Green Island", "The Kingdom in the Sky", "The Desert Kingdom", "The Golden Land", "B")
    };
}

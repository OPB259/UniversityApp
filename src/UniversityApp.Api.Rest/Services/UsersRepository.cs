namespace UniversityApp.Api.Rest.Services;

public class UsersRepository : IUsersRepository
{
    // demo: 2 konta
    private readonly Dictionary<string, (string Password, string Role)> _users = new()
    {
        ["wsei"] = ("wsei", "Admin"),
        ["student"] = ("student", "User")
    };

    public bool AuthorizeUser(string username, string password) =>
        _users.TryGetValue(username, out var u) && u.Password == password;

    public string GetRole(string username) =>
        _users.TryGetValue(username, out var u) ? u.Role : "User";
}

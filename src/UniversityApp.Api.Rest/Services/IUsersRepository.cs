namespace UniversityApp.Api.Rest.Services;

public interface IUsersRepository
{
    bool AuthorizeUser(string username, string password);
    string GetRole(string username);
}

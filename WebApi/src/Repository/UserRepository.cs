using System.Collections.Generic;
using System.Linq;
using WebApi.Models; 

namespace WebApi.Repositories
{
    public static class UserRepository
    {
        private static readonly List<User> users = new();

        public static User? Find(string username) =>
            users.FirstOrDefault(u => string.Equals(u.Username, username, System.StringComparison.OrdinalIgnoreCase));

        public static void Add(User user)
        {
            user.Id = users.Count + 1;
            users.Add(user);
        }
    }
}
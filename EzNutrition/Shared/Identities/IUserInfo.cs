using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EzNutrition.Shared.Identities
{
    public interface IUserInfo
    {
        string UserName { get; }
        string[] Roles { get; }
        string Email { get; }
    }

    public class RegistrationMessage : IUserInfo
    {
        public string Email { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string[] Roles { get; set; } = Array.Empty<string>();

        public string? MainPracticeInstitution { get; set; }

        public string? MainPracticeAreas { get; set; }
    }
}

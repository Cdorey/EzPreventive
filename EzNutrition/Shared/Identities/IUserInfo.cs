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
}

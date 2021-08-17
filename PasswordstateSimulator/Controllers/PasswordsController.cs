using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace PasswordstateSimulator.Controllers
{
    [ApiController]
    public class PasswordsController : ControllerBase
    {
        private const string APIKey = "APIKey";

        [HttpGet("api/passwords/{listId}")]
        public IActionResult Get(long listId)
        {
            if (!Request.Headers.ContainsKey("APIKey") || !Request.Headers["APIKey"].Equals(APIKey))
            {
                return Unauthorized();
            }
            
            return Ok(new List<PasswordItem>
            {
                new PasswordItem
                {
                    PasswordID = 100,
                    Title = "DB",
                    UserName = "username",
                    Password = "P@ssword!",
                    GenericField1 = $"List ID {listId}"
                },
                new PasswordItem
                {
                    PasswordID = 101,
                    Title = "Queues",
                    UserName = "username2",
                    Password = "P@ssword!2",
                    HostName = "queue-host"
                },
            });
        }
    }
}
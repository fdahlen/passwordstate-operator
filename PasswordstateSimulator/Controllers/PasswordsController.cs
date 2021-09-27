using System;
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
                new()
                {
                    PasswordID = 100,
                    Title = "Elastic",
                    UserName = "username",
                    Password = "P@ssword!",
                    GenericField1 = $"List ID {listId}",
                    GenericField2 = Convert.ToBoolean(Environment.GetEnvironmentVariable("ALWAYS_NEW_DATA"))
                        ? new Random().Next().ToString()
                        : ""
                },
                new()
                {
                    PasswordID = 101,
                    Title = "DB",
                    GenericField1 = "connection;string",
                    GenericField2 = Convert.ToBoolean(Environment.GetEnvironmentVariable("ALWAYS_NEW_DATA"))
                        ? new Random().Next().ToString()
                        : ""
                },
                new()
                {
                    PasswordID = 102,
                    Title = "FTP",
                    UserName = "username",
                    Password = "P@ssword!",
                    URL = "ftp.dummy.com",
                    GenericField2 = Convert.ToBoolean(Environment.GetEnvironmentVariable("ALWAYS_NEW_DATA"))
                        ? new Random().Next().ToString()
                        : ""
                },
            });
        }
    }
}
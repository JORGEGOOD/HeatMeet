using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SharedModels;

namespace HeatMeetServer
{
    public partial class Program
    {
        static NetworkMessage ProcessCommand(NetworkMessage message)
        {
            NetworkMessage response = new NetworkMessage { Command = message.Command };
            try
            {
                switch (message.Command)
                {
                    case "ACK":
                        //this means "Acknowleded" or Sucess, here should be nothing or a Log message.
                        Console.WriteLine("Anser received: ACK");
                        break;

                    case "LOGIN":
                        if (message.Data is JsonElement loginData)
                        {
                            string email = loginData.GetProperty("email").GetString() ?? "";
                            string password = loginData.GetProperty("password").GetString() ?? "";

                            var user = ormManager.Users.FirstOrDefault(u => u.Email == email || u.Name == email);

                            if (user == null)
                                response.Data = new { success = false, message = "User doesn't exists", userId = 0, userName = "" };
                            else if (user.Password != password)
                                response.Data = new { success = false, message = "Incorrect password", userId = 0, userName = "" };
                            else
                                response.Data = new { success = true, message = "Login correct", userId = user.Id, userName = user.Name };
                        }
                        else response.Data = new { success = false, message = "Invalid data", userId = 0, userName = "" };
                        break;

                    case "REGISTER":
                        if (message.Data is JsonElement registerData)
                        {
                            string name = registerData.GetProperty("name").GetString() ?? "";
                            string email = registerData.GetProperty("email").GetString() ?? "";
                            string password = registerData.GetProperty("password").GetString() ?? "";

                            var exists = ormManager.Users.FirstOrDefault(u => u.Email == email);

                            if (exists != null)
                                response.Data = new { success = false, message = "Email already registered" };
                            else
                            {
                                ormManager.Users.Add(new Users { Name = name, Email = email, Password = password });
                                ormManager.SaveChanges();
                                response.Data = new { success = true, message = "User registered correctly" };
                            }
                        }
                        else response.Data = new { success = false, message = "Invalid data" };

                        break;

                    case "CREATE_GROUP":
                        if (message.Data is JsonElement createGroupData)
                        {
                            string groupName = createGroupData.GetProperty("groupName").GetString() ?? "";
                            int adminId = createGroupData.GetProperty("userId").GetInt32();

                            using var ormManager = new OrmManager();
                            var user = ormManager.Users.FirstOrDefault(u => u.Id == adminId);

                            if (user == null)
                                response.Data = new { success = false, message = "User not found", inviteCode = "" };
                            else
                            {
                                var newGroup = new Groups
                                {
                                    Name = groupName,
                                    InviteCode = Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
                                    CreateDate = DateTime.UtcNow
                                };
                                newGroup.Users.Add(user);
                                ormManager.Groups.Add(newGroup);
                                ormManager.SaveChanges();
                                response.Data = new { success = true, message = "Group created", inviteCode = newGroup.InviteCode };
                            }
                        }
                        else response.Data = new { success = false, message = "Invalid data", inviteCode = "" };
                        break;
                    case "JOIN_GROUP":
                        if (message.Data is JsonElement joinGroupData)
                        {
                            string inviteCode = joinGroupData.GetProperty("inviteCode").GetString() ?? "";
                            int userId = joinGroupData.GetProperty("userId").GetInt32();


                            var group = ormManager.Groups.Include(g => g.Users).FirstOrDefault(g => g.InviteCode == inviteCode);
                            var user = ormManager.Users.FirstOrDefault(u => u.Id == userId);

                            if (group == null)
                                response.Data = new { success = false, message = "Group not found" };
                            else if (user == null)
                                response.Data = new { success = false, message = "User not found" };
                            else if (group.Users.Any(u => u.Id == userId))
                                response.Data = new { success = false, message = "Already in this group" };
                            else
                            {
                                group.Users.Add(user);
                                ormManager.SaveChanges();
                                response.Data = new { success = true, message = "Joined group correctly" };
                            }
                        }
                        else response.Data = new { success = false, message = "Invalid data" };

                        break;
                    case "GET_USER_GROUPS":
                        if (message.Data is JsonElement userGroupsData)
                        {
                            int userId = userGroupsData.GetProperty("userId").GetInt32();
                            Users? user = ormManager.Users
                                .Include(u => u.Groups)
                                .FirstOrDefault(u => u.Id == userId);

                            if (user == null || user.Groups == null || !user.Groups.Any())
                                response.Data = new { success = false, message = "No groups found" };
                            else
                                response.Data = new
                                {
                                    success = true,
                                    groups = user.Groups.Select(g => new
                                    {
                                        g.Id,
                                        g.Name,
                                        g.InviteCode,
                                        g.CreateDate
                                    }).ToList()
                                };
                        }
                        else response.Data = new { success = false, message = "Invalid data" };

                        break;
                    case "GET_GROUP_MESSAGES":

                        if (message.Data is JsonElement groupMessages)
                        {
                            int groupId = groupMessages.GetProperty("groupId").GetInt32();

                            //now we do select to the database and retreat the messages, put in into a json and send back
                            var messages = ormManager.Messages.Where(m => m.GroupId == groupId).Select(m => new { m.Content, m.CreateDate, m.UserId, UserName = m.User.Name }).ToList();
                            if (messages == null || messages.Count == 0)
                                response.Data = new { success = false, messages = new List<object>() };

                            else
                            {
                                //send all messages
                                response.Data = new
                                {
                                    success = true,
                                    messages = messages
                                };
                            }
                        }
                        else response.Data = new { success = false, message = "Invalid data" };
                        break;


                    case "SEND_CHAT_MESSAGE":
                        if(message.Data is JsonElement userMessages)
                        {
                            //messages en el sql tiene id, content, createDate, UserId, GroupId
                            //id y create date se genera en el server

                            //El cliente enviaría algo ásí
                            //NetworkMessage messages = new NetworkMessage
                            //{
                            //    Command = "GET_GROUP_MESSAGES",
                            //    Data = new
                            //    {
                            //        Id,
                            //        Content,
                            //        CreateDate,
                            //        UserId,
                            //        GroupId
                            //    }
                            //};

                            try
                            {
                                string content = userMessages.GetProperty("content").GetString();
                                int userId = userMessages.GetProperty("userId").GetInt32();
                                int groupId = userMessages.GetProperty("groupId").GetInt32();

                                //construct message
                                Messages newMessage = new Messages
                                {
                                    Content = content,
                                    UserId = userId,
                                    GroupId = groupId,
                                    CreateDate = DateTime.UtcNow
                                };

                                //save to orm
                                ormManager.Messages.Add(newMessage);
                                ormManager.SaveChanges();
                                
                                response.Data = new { success = true, messageId = newMessage.Id, newMessage.CreateDate };
                            }
                            catch (Exception ex)
                            {
                                response.Data = new {success = false, message = "Error message: " +  ex.Message};
                            }
                        }
                        else response.Data = new { success = false, message = "Invalid data" };
                        break;



                    default:
                        response.Data = new { success = false, message = "Unknown command" };
                        break;
                }
            }
            catch (Exception ex)
            {
                response.Data = new { success = false, message = "Error: " + ex.Message };
            }

            return response;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
                        {
                            //nothing here
                        }
                        break;
                    case "LOGIN":
                        {
                            if (message.Data is JsonElement loginData)
                            {
                                string email = loginData.GetProperty("email").GetString() ?? "";
                                string password = loginData.GetProperty("password").GetString() ?? "";

                                lock (ormLock)
                                {
                                    Users? user = ormManager.Users.FirstOrDefault(u => u.Email == email || u.Name == email);


                                    if (user == null)
                                        response.Data = new { success = false, message = "User doesn't exists", userId = 0, userName = "" };
                                    else if (user.Password != password)
                                        response.Data = new { success = false, message = "Incorrect password", userId = 0, userName = "" };
                                    else
                                        response.Data = new { success = true, message = "Login correct", userId = user.Id, userName = user.Name };
                                }
                            }
                            else response.Data = new { success = false, message = "Invalid data", userId = 0, userName = "" };
                        }
                        break;
                    case "REGISTER":
                        {
                            if (message.Data is JsonElement registerData)
                            {
                                string name = registerData.GetProperty("name").GetString() ?? "";
                                string email = registerData.GetProperty("email").GetString() ?? "";
                                string password = registerData.GetProperty("password").GetString() ?? "";
                                lock (ormLock)
                                {
                                    Users? exists = ormManager.Users.FirstOrDefault(u => u.Email == email);

                                    if (exists != null)
                                        response.Data = new { success = false, message = "Email already registered" };
                                    else
                                    {
                                        ormManager.Users.Add(new Users { Name = name, Email = email, Password = password });
                                        ormManager.SaveChanges();
                                        response.Data = new { success = true, message = "User registered correctly" };
                                    }
                                }
                            }
                            else response.Data = new { success = false, message = "Invalid data" };
                        }
                        break;
                    case "CREATE_GROUP":
                        {
                            if (message.Data is JsonElement createGroupData)
                            {
                                string groupName = createGroupData.GetProperty("groupName").GetString() ?? "";
                                int adminId = createGroupData.GetProperty("userId").GetInt32();

                                lock (ormLock)
                                {
                                    Users? user = ormManager.Users.FirstOrDefault(u => u.Id == adminId);

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
                                        response.Data = new { success = true, message = "Group created", inviteCode = newGroup.InviteCode, groupId = newGroup.Id, groupName = newGroup.Name };
                                    }
                                }
                            }
                            else response.Data = new { success = false, message = "Invalid data", inviteCode = "" };
                        }
                        break;
                    case "JOIN_GROUP":
                        {
                            if (message.Data is JsonElement joinGroupData)
                            {
                                string inviteCode = joinGroupData.GetProperty("inviteCode").GetString() ?? "";
                                int userId = joinGroupData.GetProperty("userId").GetInt32();

                                lock (ormLock)
                                {
                                    Groups? group = ormManager.Groups.Include(g => g.Users).FirstOrDefault(g => g.InviteCode == inviteCode);
                                    Users? user = ormManager.Users.FirstOrDefault(u => u.Id == userId);

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
                                        response.Data = new { success = true, message = "Joined group correctly", groupId = group.Id, groupName = group.Name };
                                    }
                                }
                            }
                            else response.Data = new { success = false, message = "Invalid data" };
                        }
                        break;
                    case "GET_USER_GROUPS":
                        {
                            if (message.Data is JsonElement userGroupsData)
                            {
                                int userId = userGroupsData.GetProperty("userId").GetInt32();
                                lock (ormLock)
                                {
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
                            }
                            else response.Data = new { success = false, message = "Invalid data" };
                        }
                        break;
                    case "GET_GROUP_MESSAGES":
                        {
                            if (message.Data is JsonElement groupMessages)
                            {
                                int groupId = groupMessages.GetProperty("groupId").GetInt32();
                                lock (ormLock)
                                {
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
                            }
                            else response.Data = new { success = false, message = "Invalid data" };
                        }
                        break;
                    case "SEND_CHAT_MESSAGE":
                        {
                            if (message.Data is JsonElement userMessages)
                            {
                                try
                                {
                                    //get json data
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

                                    lock (ormLock)
                                    {
                                        //save to orm
                                        ormManager.Messages.Add(newMessage);
                                        ormManager.SaveChanges();
                                        Console.WriteLine($@"New message saved to Orm from {userId}: {content}");
                                        //send success to client AND message Id
                                        response.Data = new { success = true, newId = newMessage.Id };
                                    }
                                }
                                catch (Exception ex)
                                {
                                    string? realError = ex.InnerException?.Message ?? ex.Message;
                                    Console.WriteLine($"DATABASE ERROR: {realError}");

                                    response.Data = new { success = false, message = "DB Error: " + realError };
                                }
                            }
                            else response.Data = new { success = false, message = "Invalid data" };
                        }
                        break;
                    case "RELOAD_CHAT_MESSAGES"://This returns the last message the server has track of
                        {
                            if (message.Data is JsonElement syncData)
                            {
                                int gId = syncData.GetProperty("groupId").GetInt32();
                                int lastId = syncData.GetProperty("lastId").GetInt32();

                                lock (ormLock)
                                {
                                    //Search messages the user DOESN'T have
                                    var nuevosMensajes = ormManager.Messages
                                    .Where(m => m.GroupId == gId && m.Id > lastId)
                                    .OrderBy(m => m.CreateDate)
                                    .Select(m => new
                                    {
                                        m.Id,
                                        m.Content,
                                        m.CreateDate,
                                        m.UserId,
                                        UserName = m.User.Name
                                    })
                                    .ToList();
                                    response.Data = new { success = true, messages = nuevosMensajes };
                                }
                            }
                        }
                        break;
                    case "GET_GROUP_CODE":
                        {
                            if(message.Data is JsonElement userGroupData)
                            {
                                //get groupId
                                int? groupId = userGroupData.GetProperty("groupId").GetInt32();
                                lock (ormLock)
                                {
                                    //select the group code
                                    string? inviteCode = ormManager.Groups.FirstOrDefault(m => m.Id == groupId).InviteCode;
                                    //return data
                                    response.Data = new {success =true, inviteCode = inviteCode};
                                }
                            }
                        }
                        break;
                    case "CREATE_EVENT":
                        {
                            if (message.Data is JsonElement evtData)
                            {
                                try
                                {
                                    string title = evtData.GetProperty("title").GetString() ?? "";
                                    string? ubicacion = evtData.TryGetProperty("ubicacion", out var u) ? u.GetString() : null;
                                    string? direccion = evtData.TryGetProperty("direccionUrl", out var d) ? d.GetString() : null;
                                    DateTime fechaHora = evtData.GetProperty("fechaHora").GetDateTime();
                                    int groupId = evtData.GetProperty("groupId").GetInt32();

                                    lock (ormLock)
                                    {
                                        Groups? group = ormManager.Groups.Find(groupId);

                                        if (group == null)
                                        {
                                            response.Data = new { success = false, message = "Group not found" };
                                            break;
                                        }

                                        Events newEvent = new Events
                                        {
                                            Title = title,
                                            Location = ubicacion,
                                            AddressUrl = direccion,
                                            Date = fechaHora,
                                            GroupId = groupId
                                        };

                                        ormManager.Events.Add(newEvent);
                                        ormManager.SaveChanges();

                                        Console.WriteLine($"Event '{title}' created (id={newEvent.Id}) in group {groupId}");
                                        response.Data = new { success = true, eventId = newEvent.Id };
                                    }
                                }
                                catch (Exception ex)
                                {
                                    string? realError = ex.InnerException?.Message ?? ex.Message;
                                    Console.WriteLine($"DATABASE ERROR CREATE_EVENT: {realError}");
                                    response.Data = new { success = false, message = "DB Error: " + realError };
                                }
                            }
                            else response.Data = new { success = false, message = "Invalid data" };
                        }
                        break;

                    case "GET_USER_EVENTS_AND_AVIABILITY":
                        {
                            if (message.Data is JsonElement evtData)
                            {
                                try
                                {
                                    //POR HACER
                                    
                                }
                                catch (Exception ex)
                                {
                                    string? realError = ex.InnerException?.Message ?? ex.Message;
                                    Console.WriteLine($"DATABASE ERROR GET USER EVENTS: {realError}");
                                    response.Data = new { success = false, message = "DB Error: " + realError };
                                }
                            }
                            else response.Data = new { success = false, message = "Invalid data" };
                        }
                        break;

                    default:
                        {
                            Console.WriteLine(@$"Unkown command {response.Command}");
                            response.Data = new { success = false, message = "Unknown command" };
                        }
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

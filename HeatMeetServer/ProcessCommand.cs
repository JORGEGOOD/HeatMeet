using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using SharedModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
                    case "GET_GROUP_MESSAGES_AND_EVENTS":
                        {
                            if (message.Data is JsonElement groupMessages)
                            {
                                int groupId = groupMessages.GetProperty("groupId").GetInt32();
                                lock (ormLock)
                                {
                                    //MESSAGES
                                    //select and retreat messages,into a json and send back
                                    var messages = ormManager.Messages.Where(m => m.GroupId == groupId).Select(m => new { m.Content, m.CreateDate, m.UserId, UserName = m.User.Name }).ToList();
                                    //if no messages null list
                                    if (messages == null || messages.Count == 0) response.Data = new { success = false, messages = new List<object>() };
                                    //EVENTS
                                    //var events = ormManager.Events.Where(e => e.GroupId == groupId && e.IsEvent == true).Select(e => new { e.Id, e.IsEvent });

                                    else
                                    {
                                        //send all messages
                                        response.Data = new
                                        {
                                            success  = true,
                                            messages = messages,
                                            events   = new List<object>()
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
                                    string? content = userMessages.GetProperty("content").GetString();
                                    int userId      = userMessages.GetProperty("userId").GetInt32();
                                    int groupId     = userMessages.GetProperty("groupId").GetInt32();

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
                                    string title           = evtData.GetProperty("title").GetString() ?? "";
                                    string? ubicacion      = evtData.TryGetProperty("ubicacion", out var u) ? u.GetString() : null;
                                    string? direccion      = evtData.TryGetProperty("direccionUrl", out var d) ? d.GetString() : null;
                                    DateTime rawDate       = evtData.GetProperty("fechaHora").GetDateTime();
                                    DateTime fechaHora     = DateTime.SpecifyKind(rawDate, DateTimeKind.Utc);
                                    DateTime createRawDate = evtData.GetProperty("createDate").GetDateTime();
                                    DateTime createDate    = DateTime.SpecifyKind(createRawDate, DateTimeKind.Utc);
                                    int groupId            = evtData.TryGetProperty("groupId", out var g) ? g.GetInt32(): 0;
                                    bool IsEvent           = evtData.GetProperty("isEvent").GetBoolean();
                                    
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
                                            CreateDate = createDate,
                                            GroupId = groupId,
                                            IsEvent = IsEvent,
                                            IsDrafT = true   
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
                                    int userId = evtData.GetProperty("userId").GetInt32();
                                    lock (ormLock)
                                    {
                                        //search user's groups
                                        List<int>? userGroupsIds = ormManager.Groups
                                            .Where(g => g.Users.Any(u => u.Id == userId))
                                            .Select(g=>g.Id).ToList();

                                        //get all events the groups have
                                        var all = ormManager.Events
                                                .Where(e =>
                                                    (e.GroupId != null && userGroupsIds.Contains(e.GroupId.Value) && !e.IsDrafT)
                                                    || (e.UserId == userId && !e.IsEvent)
                                                )
                                                .Select(e => new
                                                {
                                                    id = e.Id,
                                                    userId = e.UserId,
                                                    title = e.Title,
                                                    date = e.Date,
                                                    isEvent = e.IsEvent,
                                                    isAllDay = e.IsAllDay,
                                                    groupId = e.GroupId
                                                })
                                                .ToList();

                                        response.Data = new { success = true, events = all };
                                    }
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

                    case "SAVE_AVIABILITY":
                        {
                            if (message.Data is JsonElement avbData)
                            {
                                try
                                {
                                    int userId = avbData.GetProperty("userId").GetInt32();
                                    DateTime date = DateTime.SpecifyKind(avbData.GetProperty("dateSelected").GetDateTime(), DateTimeKind.Utc);
                                    bool isAllDay = avbData.GetProperty("isAllDay").GetBoolean();
                                    lock (ormLock)
                                    {
                                        //this is the same as the front, we search if it exists,
                                        //then create/delete accordingly          
                                        Events? exists = ormManager.Events//            V V V V if this event exists V V V V
                                                         .FirstOrDefault(e=>e.UserId == userId && e.Date ==date && !e.IsEvent);

                                        if (exists != null)
                                        {//if disponibility exists, we delete it
                                            Console.WriteLine("---- DELETE AVAILABILITY ----");
                                            Console.WriteLine($"Found existing event:");
                                            Console.WriteLine($"Id: {exists.Id}");
                                            Console.WriteLine($"UserId: {exists.UserId}");
                                            Console.WriteLine($"Date (DB): {exists.Date:O}");
                                            Console.WriteLine($"Date (Incoming): {date:O}");
                                            Console.WriteLine($"IsAllDay: {exists.IsAllDay}");
                                            Console.WriteLine($"IsEvent: {exists.IsEvent}");
                                            ormManager.Events .Remove(exists);
                                        }
                                        else
                                        {//if disponibility doesn't exists, we create it
                                            Console.WriteLine("---- CREATE AVAILABILITY ----");
                                            Console.WriteLine($"Creating new event with:");
                                            Console.WriteLine($"UserId: {userId}");
                                            Console.WriteLine($"Date: {date:O}");
                                            Console.WriteLine($"IsAllDay: {isAllDay}");
                                            Events newEvent = new Events
                                            {
                                                UserId = userId,
                                                Date = date,
                                                IsEvent = false,
                                                IsAllDay = isAllDay, 
                                                Title = isAllDay ? "🔴 Disponible" : "🔴 Disponible (Hora)",
                                                GroupId = null
                                            };
                                            ormManager.Events.Add(newEvent);
                                        }
                                        ormManager.SaveChanges();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    string? realError = ex.InnerException?.Message ?? ex.Message;
                                    Console.WriteLine($"DATABASE ERROR SAVE AVIABILITY: {realError}");
                                    response.Data = new { success = false, message = "DB Error: " + realError };
                                }
                            }
                            else response.Data = new { success = false, message = "Invalid data" };
                        }
                        break;
                    case "GET_LAST_EVENT":
                        {
                            if (message.Data is JsonElement evtData)
                            {
                                int groupId = evtData.GetProperty("groupId").GetInt32();
                                lock (ormLock)
                                {
                                    Events? lastEvent = ormManager.Events
                                        .Where(e => e.GroupId == groupId && e.IsEvent == true && e.IsDrafT == true)
                                        .OrderByDescending(e => e.Date)
                                        .FirstOrDefault();

                                    if (lastEvent == null)
                                        response.Data = new { success = false, message = "No draft events found" };
                                    else
                                        response.Data = new
                                        {
                                            success = true,
                                            eventId = lastEvent.Id,
                                            title = lastEvent.Title,
                                            fechaHora = lastEvent.Date,
                                            ubicacion = lastEvent.Location ?? "",
                                            direccionUrl = lastEvent.AddressUrl ?? ""
                                        };
                                }
                            }
                            else response.Data = new { success = false, message = "Invalid data" };
                        }
                        break;
                    case "VOTE_EVENT":
                        {
                            if (message.Data is JsonElement voteData)
                            {
                                try
                                {
                                    int eventId = voteData.GetProperty("eventId").GetInt32();
                                    int userId = voteData.GetProperty("userId").GetInt32();
                                    bool accepts = voteData.GetProperty("accepts").GetBoolean();

                                    lock (ormLock)
                                    {
                                        Events? evt = ormManager.Events
                                            .Include(e => e.Group)
                                            .ThenInclude(g => g.Users)
                                            .FirstOrDefault(e => e.Id == eventId);

                                        if (evt == null)
                                        {
                                            response.Data = new { success = false, message = "Event not found" };
                                            break;
                                        }

                                        // Si vota NO → eliminar directamente
                                        if (!accepts)
                                        {
                                            ormManager.Events.Remove(evt);
                                            ormManager.SaveChanges();
                                            response.Data = new { success = true, result = "deleted" };
                                            break;
                                        }

                                        // Si vota SI → guardar voto
                                        bool alreadyVoted = ormManager.Votes
                                            .Any(v => v.EventId == eventId && v.UserId == userId);

                                        if (!alreadyVoted)
                                        {
                                            ormManager.Votes.Add(new Votes
                                            {
                                                EventId = eventId,
                                                UserId = userId,
                                                Date = DateTime.UtcNow,
                                                HourStart = TimeSpan.Zero,
                                                HourEnd = TimeSpan.Zero
                                            });
                                            ormManager.SaveChanges();
                                        }

                                        // Comprobar si todos han votado
                                        int totalMembers = evt.Group?.Users?.Count ?? 0;
                                        int totalVotes = ormManager.Votes.Count(v => v.EventId == eventId);

                                        if (totalMembers > 0 && totalVotes >= totalMembers)
                                        {
                                            // Todos votaron SI → confirmar evento
                                            evt.IsDrafT = false;
                                            ormManager.SaveChanges();
                                            response.Data = new { success = true, result = "confirmed" };
                                        }
                                        else
                                        {
                                            response.Data = new { success = true, result = "voted", votes = totalVotes, total = totalMembers };
                                        }
                                    }

                                    // Timer 2 minutos → eliminar si sigue siendo draft
                                    int capturedId = voteData.GetProperty("eventId").GetInt32();
                                    Task.Run(async () =>
                                    {
                                        await Task.Delay(TimeSpan.FromMinutes(2));
                                        lock (ormLock)
                                        {
                                            Events? draft = ormManager.Events.Find(capturedId);
                                            if (draft != null && draft.IsDrafT)
                                            {
                                                ormManager.Events.Remove(draft);
                                                ormManager.SaveChanges();
                                                Console.WriteLine($"Draft event {capturedId} auto-deleted after 2 minutes.");
                                            }
                                        }
                                    });
                                }
                                catch (Exception ex)
                                {
                                    string? realError = ex.InnerException?.Message ?? ex.Message;
                                    response.Data = new { success = false, message = "DB Error: " + realError };
                                }
                            }
                            else response.Data = new { success = false, message = "Invalid data" };
                        }
                        break;

                    case "GET_GROUP_AVAILABILITY":
                        {
                            if (message.Data is JsonElement evtData)
                            {
                                int groupId = evtData.GetProperty("groupId").GetInt32();
                                lock (ormLock)
                                {
                                    
                                    List<int> memberIds = ormManager.Groups
                                        .Include(g => g.Users)
                                        .FirstOrDefault(g => g.Id == groupId)
                                        ?.Users.Select(u => u.Id).ToList() ?? new List<int>();

                                   
                                    var availabilities = ormManager.Events
                                        .Where(e => memberIds.Contains(e.UserId) && !e.IsEvent)
                                        .Select(e => new
                                        {
                                            userId = e.UserId,
                                            date = e.Date,
                                            isAllDay = e.IsAllDay,
                                            title = e.Title
                                        })
                                        .ToList();

                                    response.Data = new { success = true, availabilities };
                                }
                            }
                            else response.Data = new { success = false, message = "Invalid data" };
                        }
                        break;

                    case "LEAVE_GROUP":
                        {
                            if (message.Data is JsonElement leaveData)
                            {
                                int groupId = leaveData.GetProperty("groupId").GetInt32();
                                int userId = leaveData.GetProperty("userId").GetInt32();

                                lock (ormLock)
                                {
                                    Groups? group = ormManager.Groups
                                        .Include(g => g.Users)
                                        .FirstOrDefault(g => g.Id == groupId);

                                    Users? user = ormManager.Users.FirstOrDefault(u => u.Id == userId);

                                    if (group == null)
                                    {
                                        response.Data = new { success = false, message = "Group not found" };
                                    }
                                    else if (user == null)
                                    {
                                        response.Data = new { success = false, message = "User not found" };
                                    }
                                    else if (!group.Users.Any(u => u.Id == userId))
                                    {
                                        response.Data = new { success = false, message = "User is not in this group" };
                                    }
                                    else
                                    {
                                        group.Users.Remove(user);
                                        ormManager.SaveChanges();

                                        response.Data = new { success = true, message = "User left the group" };
                                    }
                                }
                            }
                            else
                            {
                                response.Data = new { success = false, message = "Invalid data" };
                            }
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


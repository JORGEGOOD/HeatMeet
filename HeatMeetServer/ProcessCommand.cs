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
                                int groupId = groupMessages.GetProperty("groupId").GetInt32();//get groupId for the database select
                                lock (ormLock)
                                {
                                    //MESSAGES
                                    var messages = ormManager.Messages.Where(m => m.GroupId == groupId).Select(m => new { m.Content, m.CreateDate, m.UserId, UserName = m.User.Name }).ToList();
                                    //EVENTS
                                    var events = ormManager.Events.Where(e => e.GroupId == groupId && e.IsEvent == true)
                                                .Select(e => new{ e.Id, e.Title, e.Location, e.Date, e.CreateDate, e.IsEvent, e.IsAllDay,e.UserId}).ToList();
                                    //send
                                    response.Data = new
                                    {
                                        success  = true,
                                        messages = messages, //.ToList() always returns a list even if null
                                        events   = events  
                                    };
                                    
                                }
                            }
                            else response.Data = new { success = false, message = "Invalid data"};
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
                            Console.WriteLine("RELOAD CHAT MESSAGES FUNCTION LLAMADO");
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
                                            IsDraft = true   
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
                                                    (e.GroupId != null && userGroupsIds.Contains(e.GroupId.Value) && !e.IsDraft)
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
                                                Title = isAllDay ? " Disponible" : " Disponible (Hora)",
                                                GroupId = null,
                                                
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
                    case "GET_LAST_EVENT"://Is this used? In future we could kpi this
                        {
                            if (message.Data is JsonElement evtData)
                            {
                                int groupId = evtData.GetProperty("groupId").GetInt32();
                                lock (ormLock)
                                {
                                    Events? lastEvent = ormManager.Events
                                        .Where(e => e.GroupId == groupId && e.IsEvent == true && e.IsDraft == true)
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

                    case "GET_EVENT":
                        {
                            if (message.Data is JsonElement evtData)
                            {
                                int groupId = evtData.GetProperty("groupId").GetInt32();
                                int eventId = evtData.GetProperty("eventId").GetInt32();
                                lock (ormLock)
                                {
                                    //get event by id
                                    Events? Event = ormManager.Events
                                                    .Where(e => e.Id == eventId && e.GroupId == groupId)
                                                    .FirstOrDefault();

                                    if (Event == null) response.Data = new { success = false, message = "No draft events found" };
                                    else
                                    response.Data = new
                                    {
                                        success = true,
                                        eventId = Event.Id,
                                        title = Event.Title,
                                        fechaHora = Event.Date,
                                        ubicacion = Event.Location ?? "",
                                        direccionUrl = Event.AddressUrl ?? ""
                                    };
                                }
                            }
                            else response.Data = new { success = false, message = "Invalid data" };
                        }
                        break;

                    case "GET_EVENT_PROPOSALS":
                        if (message.Data is JsonElement msgData)
                        {
                            try
                            {
                                /*-- FUNCTIONALITY -- 
                                We get the date creator put as the main proposal.
                                
                                Then we offer 3 more disponible days according to
                                the group's aviability.
                                 -------------------*/



                                int eventId = msgData.GetProperty("eventId").GetInt32();
                                lock (ormLock)
                                {
                                    //Get event
                                    Events? evtPropuesta = ormManager.Events
                                        .Include(e => e.Group)
                                        .ThenInclude(g => g.Users)
                                        .FirstOrDefault(e => e.Id == eventId);

                                    if (evtPropuesta == null || evtPropuesta.Group == null)
                                    {
                                        response.Data = new { success = false, message = "Evento o grupo no encontrado" };
                                        break;
                                    }

                                    //Get all users from group
                                    List<int> memberIds = evtPropuesta.Group.Users.Select(u => u.Id).ToList();


                                    //HUGE dateTime errors were here, new readers won't understand the pain this caused
                                    DateTime today = DateTime.UtcNow.Date;
                                    var topDays = ormManager.Events  //get days with most disponibility from the future
                                        .Where(e => memberIds.Contains(e.UserId) &&
                                                    e.IsEvent == false &&
                                                    e.Date >= today)
                                        .GroupBy(e => e.Date)
                                        .Select(g => new
                                        {
                                            Fecha = g.Key,
                                            Count = g.Count()
                                        })
                                        .OrderByDescending(x => x.Count)
                                        .Take(3)
                                        .ToList();

                                    //Add the creator proposal
                                    var creatorDate  = evtPropuesta.Date.Date;
                                    int creatorCount = ormManager.Events.Count(e => memberIds.Contains
                                                                              (e.UserId) && !e.IsEvent && e.Date.Date == creatorDate);

                                    //if creatorProposal and one of the 3 proposals are the same, logic is manager in front
                                    response.Data = new { success = true, topDays = topDays /*, creatorProposal = new { creatorDate, creatorCount } */};
                                }
                            }
                            catch (Exception ex)
                            {
                                response.Data = new { success = false, message = ex.Message };
                            }
                        }
                        break;
                    case "VOTE_EVENT":
                        {
                            if (message.Data is JsonElement voteData)
                            {
                                try
                                {
                                    int userId  = voteData.GetProperty("userId").GetInt32();
                                    int eventId = voteData.GetProperty("eventId").GetInt32();
                                    DateTime selectedDate = voteData.GetProperty("selectedDate").GetDateTime().Date;

                                    lock(ormLock)
                                    {
                                        //get event
                                        Events? evt = ormManager.Events.Include(e => e.Group).ThenInclude(g => g.Users)
                                                      .FirstOrDefault(e => e.Id == eventId);
                                        if (evt == null) { response.Data = new { success = false, message = "Evento no encontrado" }; break; }

                                        //Check if user has already voted
                                        Votes? existingVote = ormManager.Votes.FirstOrDefault(v => v.EventId == eventId && v.UserId == userId);

                                        //if vote already exists, update
                                        if(existingVote != null) existingVote.Date = selectedDate;
                                        else//if vote not exists, create vote
                                        {
                                            ormManager.Votes.Add(new Votes
                                            {
                                                EventId = eventId,
                                                UserId = userId,
                                                Date = selectedDate,
                                                HourStart = TimeSpan.Zero,
                                                HourEnd = TimeSpan.Zero
                                            });

                                        }
                                        ormManager.SaveChanges();

                                        //Check if everyone has voted
                                        //get total group members
                                        int totalMembers = evt.Group?.Users?.Count ?? 0;
                                        //get all votes
                                        List<Votes> allVotes = ormManager.Votes.Where(v => v.EventId == eventId).ToList();

                                        //if theres more votes than members (everyone voted)
                                        if (totalMembers > 0 && allVotes.Count >= totalMembers)
                                        {
                                            //get the day with most votes
                                            DateTime winnerDate = allVotes
                                                .GroupBy(v => v.Date)
                                                .OrderByDescending(g => g.Count())
                                                .ThenBy(g => g.Key)
                                                .First().Key;

                                            evt.IsDraft = false;//remove draft from event (it will now show up)
                                            evt.Date = winnerDate; //event has winner date
                                            ormManager.SaveChanges();


                                            //return to user the event has been created
                                            //If we have time we should add that this warns everyone, but too time consuming for 2 month project
                                            response.Data = new { success = true, result = "confirmed", finalDate = winnerDate };
                                            break;
                                        }
                                        
                                        //return user he voted correctly
                                        response.Data = new { success = true, result = "voted", current = allVotes.Count, total = totalMembers };
                                        
                                    }
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
                    case "GET_GROUP_AVAILABILITY"://Get all aviabilities from a group
                        {
                            if (message.Data is JsonElement evtData)
                            {
                                int groupId = evtData.GetProperty("groupId").GetInt32();
                                lock (ormLock)
                                {
                                    //get all members from the group
                                    List<int> memberIds = ormManager.Groups
                                        .Include(g => g.Users)
                                        .FirstOrDefault(g => g.Id == groupId)
                                        ?.Users.Select(u => u.Id).ToList() ?? new List<int>();

                                   //get all avuabilities from the group in a list
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

                    case "RENAME_GROUP":
                        {
                            if (message.Data is JsonElement renameData)
                            {
                                int groupId = renameData.GetProperty("groupId").GetInt32();
                                string newName = renameData.GetProperty("newName").GetString() ?? "";

                                if (string.IsNullOrWhiteSpace(newName))
                                {
                                    response.Data = new { success = false, message = "El nombre no puede estar vacío" };
                                    break;
                                }

                                lock (ormLock)
                                {
                                    Groups? group = ormManager.Groups.Find(groupId);
                                    if (group == null)
                                    {
                                        response.Data = new { success = false, message = "Grupo no encontrado" };
                                        break;
                                    }
                                    group.Name = newName;
                                    ormManager.SaveChanges();
                                    response.Data = new { success = true, newName };
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

                                    if (group == null || user == null)
                                    {
                                        response.Data = new { success = false, message = "Group or user not found" };
                                        break;
                                    }

                                    group.Users.Remove(user);
                                    ormManager.SaveChanges();
                                    response.Data = new { success = true };
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

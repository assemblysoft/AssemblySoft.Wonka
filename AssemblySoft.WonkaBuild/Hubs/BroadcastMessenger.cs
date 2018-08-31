using Microsoft.AspNet.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace AssemblySoft.WonkaBuild.Hubs
{
    /// <summary>
    /// Message status hub
    /// </summary>
    public class MessageStatusHub : Hub
    {
        private Broadcaster _broadcaster;

        public MessageStatusHub()
            : this(Broadcaster.Instance)
        {
        }

        public MessageStatusHub(Broadcaster broadcaster)
        {
            _broadcaster = broadcaster;
        }

        public void AddModel(MessageModel clientModel)
        {
            clientModel.LastUpdatedBy = Context.ConnectionId;

            //add message to the message collection within our broadcaster
            _broadcaster.AddMessage(clientModel);
        }
    }

    /// <summary>
    /// Broadcaster 
    /// </summary>
    public class Broadcaster
    {
        private readonly static Lazy<Broadcaster> _instance =
            new Lazy<Broadcaster>(() => new Broadcaster());

        private const int MAX_MESSAGES_PROCESS = 100; //ToDo: config
        private const int MAX_MESSAGES_GROUP = 25; //ToDo: config
        private readonly TimeSpan BroadcastInterval = TimeSpan.FromMilliseconds(300); //ToDo: config
        private readonly IHubContext _hubContext;
        private Timer _broadcastLoop;
        private bool _messageAvailable;
        List<MessageModel> _messages = new List<MessageModel>();

        public Broadcaster()
        {
            _hubContext = GlobalHost.ConnectionManager.GetHubContext<MessageStatusHub>();
            _messages = new List<MessageModel>();
            _messageAvailable = false;

            //start the broadcast loop
            _broadcastLoop = new Timer(
                BroadcastMessages,
                null,
                BroadcastInterval,
                BroadcastInterval
                );
        }

        private Object messageLock = new Object();
        private bool _processing;

        public int MessageCount
        {
            get
            {
                return _messages.Count;
            }
        }

        /// <summary>
        /// Broadcasts messages to all clients
        /// </summary>
        /// <param name="state"></param>
        private void BroadcastMessagesInGroups(object state)
        {
            if (_messageAvailable && !_processing)
            {
                _processing = true;

                if (!_messages.Any())
                    return;

                List<MessageModel> copy;

                var count = _messages.Count();

                if (count > MAX_MESSAGES_PROCESS)
                    count = MAX_MESSAGES_PROCESS;

                copy = _messages.Take(count).ToList(); //candidate for locking

                if (copy.Count() > MAX_MESSAGES_GROUP)
                {
                    while (copy.Count() > MAX_MESSAGES_GROUP)
                    {
                        var messages = copy.Take(MAX_MESSAGES_GROUP);
                        SendToClient(messages);
                        copy.RemoveRange(0, MAX_MESSAGES_GROUP);
                    }
                }
                else
                {
                    if (copy.Count > 0)
                    {
                        var messages = copy.ToList();
                        SendToClient(messages);
                    }
                }

                _messages.RemoveRange(0, count);
                _messageAvailable = false;
                _processing = false;
            }

        }

        /// <summary>
        /// Broadcasts messages to all clients
        /// </summary>
        /// <param name="state"></param>
        private void BroadcastMessages(object state)
        {
            if (_messageAvailable && !_processing)
            {
                _processing = true;

                if (!_messages.Any())
                    return;

                List<MessageModel> copy;
                var count = _messages.Count();

                copy = _messages.Take(count).ToList(); //candidate for locking

                foreach (var msg in copy)
                {
                    SendToClient(new List<MessageModel> { msg });
                }

                _messages.RemoveRange(0, count);
                _messageAvailable = false;
                _processing = false;
            }

        }

        /// <summary>
        /// Sends a collection of messages to the client
        /// </summary>
        /// <param name="copy"></param>
        private void SendToClient(IEnumerable<MessageModel> copy)
        {
            StringBuilder messageBuilder = new StringBuilder();
            foreach (var msg in copy)
            {
                if (!string.IsNullOrEmpty(msg.Message))
                {                    
                    messageBuilder.Append(msg.Message);
                }
            }
            dynamic model = new
            {
                message = messageBuilder.ToString(),
                status = "model updated",
            };

            _hubContext.Clients.All.addModel(model);
        }


        /// <summary>
        /// Adds a mssage to the broadcaster
        /// </summary>
        /// <param name="clientModel"></param>
        public void AddMessage(MessageModel clientModel)
        {
            _messages.Add(clientModel);
            _messageAvailable = true;
        }

        /// <summary>
        /// The broadcaster instance
        /// </summary>
        public static Broadcaster Instance
        {
            get
            {
                return _instance.Value;
            }
        }
    }

    /// <summary>
    /// The message model
    /// </summary>
    public class MessageModel
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonIgnore]
        public string LastUpdatedBy { get; set; }
    }

}
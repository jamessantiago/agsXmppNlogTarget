using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using NLog.Targets;
using NLog.Config;
using agsXMPP;
using agsXMPP.protocol.client;
using agsXMPP.Collections;
using agsXMPP.protocol.iq.roster;


namespace agsXmppNlogTarget
{
    [Target("Xmpp")]
    public class XmppTarget : TargetWithLayout
    {
        #region Properties

        [RequiredParameter]
        public string Domain { get; set; }
        [RequiredParameter]
        public string ConnectServer { get; set; }
        [RequiredParameter]
        public string Username { get; set; }
        [RequiredParameter]
        public string Password { get; set; }
        [RequiredParameter]
        public string Recipient { get; set; }


        private const int MAXERRORS = 5;
        private const int MAXDISCONNECTS = 10;
        private const int MAXSTACKEDMESSAGES = 5;
        private const int SENDFREQUENCY = 5000; //in milliseconds


        private int disconnectCount = 0;
        private int errorCount = 0;
        private Jid jidRecipient;
        private Stack<string> messageStack = new Stack<string>();
        private object messageLocker = new object();
        private XmppClientConnection xmpp { get; set; }
        private System.Timers.Timer messageTimer;

        #endregion Properties

        #region Constructor

        /// <summary>
        /// The public properties set by NLog don't seem to be set until after the constructor is called.
        /// Initiation is performed at first logging write instead.
        /// </summary>
        public XmppTarget()
        {

        }

        /// <summary>
        /// Establishes xmpp connection
        /// </summary>
        private void InitiateXmpp()
        {
            try
            {                
                jidRecipient = new Jid(Recipient);
                xmpp = new XmppClientConnection(this.Domain ?? "");
                xmpp.ConnectServer = ConnectServer ?? "";
                xmpp.Open(this.Username ?? "", this.Password ?? "");
                xmpp.OnLogin += delegate(object o) { SendPresence(); SendMessageStack(); InitializeMessageTimer(); };
                xmpp.OnXmppConnectionStateChanged += delegate(object o, XmppConnectionState connectionState) { HandleConnectionState(connectionState); };
                xmpp.OnError += new ErrorHandler(HandleError);
            }
            catch { }
        }

        #endregion Constructor

        #region Methods

        #region Stack messages as they arrive

        /// <summary>
        /// Pushes log message into message stack.  See timer for xmpp sending
        /// </summary>
        /// <param name="logEvent"></param>
        protected override void Write(LogEventInfo logEvent)
        {
            if (xmpp == null)
                InitiateXmpp();
            string logMessage = this.Layout.Render(logEvent);
            messageStack.Push(logMessage);
            if (messageStack.Count > MAXSTACKEDMESSAGES)
                messageStack.Pop();
        }

        /// <summary>
        /// Sends message stack through xmpp, combines messages if there are multiple
        /// messages in the stack
        /// </summary>
        private void SendMessageStack()
        {
            lock (messageLocker)
            {
                string messages = "";
                try
                {
                    if (messageStack.Count > 1)
                    {
                        StringBuilder sb = new StringBuilder();
                        while (messageStack.Peek() != null)
                            sb.AppendLine(messageStack.Pop());
                        messages = sb.ToString();
                    }
                    else
                        messages = messageStack.Pop();
                    
                    xmpp.Send(new Message(jidRecipient, messages));
                }
                catch { }                
            }
        }

        #endregion Stack messages as they arrive

        #region Timer based message sending

        /// <summary>
        /// Starts xmpp message sending timer
        /// </summary>
        private void InitializeMessageTimer()
        {
            if (messageTimer == null)
            {
                messageTimer = new System.Timers.Timer(SENDFREQUENCY);
                messageTimer.Elapsed += new System.Timers.ElapsedEventHandler(Timer_Elapsed);
                messageTimer.Start();
            }
        }

        private void Timer_Elapsed(object state, System.Timers.ElapsedEventArgs e)
        {
            SendMessageStack();
        }

        #endregion Timer based message sending

        #region Handle problems

        private void HandleConnectionState(XmppConnectionState connectionState)
        {
            if (connectionState == XmppConnectionState.Disconnected && disconnectCount <= MAXDISCONNECTS)
            {
                disconnectCount++;
                xmpp.Open();
            }
            else if (disconnectCount > MAXDISCONNECTS)
            {
                this.CloseTarget();
            }
        }

        private void HandleError(object o, Exception error)
        {
            errorCount++;
            if (errorCount > MAXERRORS)
                this.CloseTarget();
        }

        #endregion Handle problems

        //TODO: this doesn't seem to work, called at initiation anyway
        private void SendPresence()
        {
            Presence p = new Presence(ShowType.chat, "Available (NLog)");
            p.Type = PresenceType.available;
            xmpp.Send(p);
        }

        #endregion Methods
    }
}

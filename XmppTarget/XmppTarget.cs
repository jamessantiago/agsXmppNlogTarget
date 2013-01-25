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


        private int disconnectCount = 0;
        private int errorCount = 0;
        private Jid jidRecipient;
        private bool isAuthenticating = false;
        private Stack<string> messageStack = new Stack<string>();
        private object messageLocker = new object();
        private XmppClientConnection xmpp { get; set; }
        private System.Timers.Timer messageTimer;

        #endregion Properties

        #region Constructor

        public XmppTarget()
        {

        }

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

        protected override void Write(LogEventInfo logEvent)
        {
            if (xmpp == null)
                InitiateXmpp();
            string logMessage = this.Layout.Render(logEvent);
            messageStack.Push(logMessage);
            if (messageStack.Count > 5)
                messageStack.Pop();
        }

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

        private void InitializeMessageTimer()
        {
            if (messageTimer == null)
            {
                messageTimer = new System.Timers.Timer(5000);
                messageTimer.Elapsed += new System.Timers.ElapsedEventHandler(Timer_Elapsed);
                messageTimer.Start();
            }
        }

        private void Timer_Elapsed(object state, System.Timers.ElapsedEventArgs e)
        {
            SendMessageStack();
        }

        #region Handle problems

        private void HandleConnectionState(XmppConnectionState connectionState)
        {
            if (connectionState == XmppConnectionState.Disconnected && disconnectCount <= 5)
            {
                disconnectCount++;
                xmpp.Open();
            }
            else if (disconnectCount > 5)
            {
                this.CloseTarget();
            }
        }

        private void HandleError(object o, Exception error)
        {
            errorCount++;
            if (errorCount > 10)
                this.CloseTarget();
        }

        #endregion Handle problems

        private void SendPresence()
        {
            Presence p = new Presence(ShowType.chat, "Available (NLog)");
            p.Type = PresenceType.available;
            xmpp.Send(p);
        }

        #endregion Methods
    }
}

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using NLog.Targets;
using agsXMPP;
using agsXMPP.protocol.client;
using agsXMPP.Collections;
using agsXMPP.protocol.iq.roster;

namespace Tests_XmppTarget
{
    [TestClass]
    public class UnitTest1
    {
        ///not sure what's going on over here.  need to figure out a better test process flow
        ///...and how to do testing.......


        private string domain = "";
        private string connectserver = "";
        private string username = "";
        private string password = "";
        private string recipient = "";

        [TestMethod]
        public void XmppSendTest()
        {
            var xmpp = new XmppClientConnection(domain);
            xmpp.ConnectServer = connectserver;
            xmpp.Open(username, password);
            xmpp.OnError += new ErrorHandler(delegate { logerror(); });
            xmpp.OnLogin += delegate(object o) { xmpp.Send(new Message(new Jid(recipient), MessageType.chat, "james")); };
            while (!xmpp.Authenticated)
                System.Threading.Thread.Sleep(500);
            System.Threading.Thread.Sleep(1000);
            xmpp.Send(new Message(new Jid(recipient), MessageType.chat, "james"));
            xmpp.Close();
        }

        private void logerror()
        {
            var here = "error";
        }



    }
}

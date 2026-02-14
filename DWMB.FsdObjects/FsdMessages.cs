namespace DWMB_AIO.DWMB.FsdObjects
{
    class FsdMessage : FsdPacket
    {
        /// <summary>
        /// Contents of the message.
        /// </summary>

        public string? Message { get; }

        public FsdMessage(DateTime timestamp, string packetString) : base(timestamp, packetString)
        {
            string[] contents = packetString.Split(':');

            //prase message fields
            //format #TM<sender>:<recipient>:<message>
            for (int i = 0; i < contents.Length; i++)
            {
                switch (i)
                {
                    case 0:
                        //first field contains sender
                        base.Sender = contents[i].Substring(3); //remove #TM prefix
                        break;
                    case 1:
                        //second field contains recipient
                        //If it is a frequency message, it'll be addressed to "@xxyyy" (i.e. 1xx.yyy MHz)
                        //Keep the "@" so that tyhe server knows it's a frequency message
                        base.Recipient = contents[i];
                        break;
                    case 2:
                        //third field contains message
                        this.Message = contents[i];
                        break;
                    default:
                        //additional fields are part of the message (colons in message)
                        this.Message += ":" + contents[i];
                        break;
                }
            }
        }
    }
}

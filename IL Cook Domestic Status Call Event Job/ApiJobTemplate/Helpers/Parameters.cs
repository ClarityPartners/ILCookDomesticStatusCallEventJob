using System.Xml;
using Tyler.Odyssey.Utils;

namespace ILCookDomesticStatusCalEventJob.Helpers
{
    public class Parameters
    {
        //public string Location { get; private set; }
        //public string EventCode { get; private set; }
        public string CallType { get; private set; }

        public Parameters(XmlElement taskNode, UtilsLogger logger)
        {
            logger.WriteToLog("Beginning Parameters() constructor", LogLevel.Verbose);
            logger.WriteToLog("taskNode: " + taskNode.OuterXml, LogLevel.Verbose);

            //Location = taskNode.GetAttribute("Locations");
            //EventCode = taskNode.GetAttribute("EventCode");
            CallType = taskNode.GetAttribute("CallType");

            logger.WriteToLog("Instantiated Parameters", LogLevel.Verbose);
        }
    }
}
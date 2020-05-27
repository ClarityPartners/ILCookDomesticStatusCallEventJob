using System;
using System.Runtime.InteropServices;
using Tyler.Odyssey.JobProcessing;

namespace ILCookDomesticStatusCalEventJob
{
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("f01d0c4e-229b-4d0b-ab8f-36740590eee4")]
    [ComVisible(true)]
    public class JobTask : Task
    {
        protected override void SetupProcessor(string SiteID, string JobTaskXML)
        {
            Processor = new DataProcessor(SiteID, JobTaskXML);

            ((DataProcessor)Processor).TaskParms = this.jobTaskParms;
            ((DataProcessor)Processor).TaskUtility = this.taskUtility;
            ((DataProcessor)Processor).TaskDocument = this.taskDocument;

            UserID = ((DataProcessor)Processor).Context.UserID;
        }

        private int UserID { get; set; }
    }
}

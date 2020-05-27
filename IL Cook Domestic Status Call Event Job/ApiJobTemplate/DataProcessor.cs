using JobProcessingInterface;
using MSXML3;
using System;
using System.IO;
using Tyler.Odyssey.JobProcessing;
using Tyler.Odyssey.Utils;
using ILCookDomesticStatusCalEventJob.Helpers;
using System.Linq;
using ILCookDomesticStatusCalEventJob.Exceptions;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.Serialization.Json;
using Tyler.Odyssey.API.JobTemplate;
using Tyler.Integration.Framework;
using System.Xml.Serialization;
using Tyler.Odyssey.API.Shared;
using Tyler.Integration.General;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Generic;


namespace ILCookDomesticStatusCalEventJob
{
    internal class DataProcessor : TaskProcessor
    {
        // Constructor
        public DataProcessor(string SiteID, string JobTaskXML) : base(SiteID, JobTaskXML)
        {
            Logger.WriteToLog("JobTaskXML:\r\n" + JobTaskXML, LogLevel.Basic);

            // New up the context object
            Context = new Context(Logger);

            Logger.WriteToLog("Completed instantiation of context object", LogLevel.Verbose);

            // Retrieve the parameters for the job (which flags to add/remove)
            Context.DeriveParametersFromJobTaskXML(SiteID, JobTaskXML);
            Context.ValidateParameters();

            Logger.WriteToLog("Finished deriving parameters", LogLevel.Verbose);

            // TODO:  Add the code tables that need to be updated to the following function (Context.AddCacheItems())
            Context.AddCacheItems();
            Context.UpdateCache();

            Logger.WriteToLog("Completed cache update.", LogLevel.Verbose);
        }

        // Static constructor
        static DataProcessor()
        {
            Logger = new UtilsLogger(LogManager);
            Logger.WriteToLog("Logger Instantiated", LogLevel.Basic);
        }

        // Destructor
        ~DataProcessor()
        {
            Logger.WriteToLog("Disposing!", LogLevel.Basic);

            if (Context != null)
                Context.Dispose();
        }

        public static IUtilsLogManager LogManager = new UtilsLogManagerBase(Constants.LOG_REGISTRY_KEY);
        public static readonly UtilsLogger Logger;

        public IXMLDOMDocument TaskDocument { get; set; }

        internal Context Context { get; set; }

        public ITYLJobTaskUtility TaskUtility { get; set; }

        private object taskParms;
        public object TaskParms { get { return taskParms; } set { taskParms = value; } }

        public override void Run()
        {
            Logger.WriteToLog("Beginning Run Method", LogLevel.Basic);

            // TODO: Update API Processing Logic
            try
            {
                // Input Parameters
                
                string callType = Context.Parameters.CallType;
                DataSet caseDataSet = null;

                // Used progress call
                if(callType == "DMEXCALL")
                {
                    Logger.WriteToLog("------- Running DMEXCALL -------", LogLevel.Verbose);
                    caseDataSet = GetListOfDMEXCasesSQL();
                    DataTable listOfCasesDS = caseDataSet.Tables[0];
                    foreach (DataRow row in listOfCasesDS.Rows)
                    {
                        Logger.WriteToLog("----- Processing Case Number:  " + row[1] + " -----", LogLevel.Basic);
                        // Need fileDate Logic    
                        // CaseID, CaseNbr, DtFile, NodeID, CurrCaseStatusCode, CalendarCode, CaseTypeCode, CaseSequence
                        // If less than 27 weeks from the filing week, then we want to schedule at least 27 weeks
                        if (DateTime.Parse(row[2].ToString()).AddDays(189) > DateTime.Today)
                        {
                            string hearingEventCode = "DRCSOPC"; // This is hardcoded due to time contraints.
                            bool eventAddSuccess = AddCaseEvent(row[0].ToString(), hearingEventCode);

                            if (eventAddSuccess)
                            {
                                string eventCode = "DR4406"; // This is hardcoded due to time contraints.
                                bool eventAddSuccess2 = AddCaseEvent(row[0].ToString(), eventCode);
                                Logger.WriteToLog("Case Number " + row[1] + " processed successfully.", LogLevel.Basic);
                            }
                            else
                                Logger.WriteToLog("Case Number " + row[1] + " processed unsuccessfully.", LogLevel.Basic);
                        }
                        else // Logic to schedule hearing within file date timeframe.
                        {
                            string startDate = DateTime.Today.AddDays(189).ToString("MM/dd/yyyy");
                            string endDate = DateTime.Today.AddDays(210).ToString("MM/dd/yyyy");
                            string hearingType = "DRPROG";

                            // Find Hearing
                            string result = FindHearing(row, startDate, endDate, hearingType);
                            Logger.WriteToLog("Hearings " + result, LogLevel.Verbose);
                            if (result != "ERROR")
                            {
                                // If hearings are found, spin through the results to find the first open session.
                                string sessionID = FindFirstOpenSession(result);
                                if (sessionID != "0")
                                {
                                    ScheduleHearing(row, startDate, endDate, hearingType, sessionID);
                                    Logger.WriteToLog("Case Number " + row[1] + " processed successfully.", LogLevel.Basic);
                                }
                                else
                                    Logger.WriteToLog("Case Number " + row[1] + " processed unsuccessfully.  Cannot find an open session.", LogLevel.Basic);
                            }
                            else
                                Logger.WriteToLog("Case Number " + row[1] + " processed unsuccessfully.  Cannot find a hearing.", LogLevel.Basic);



                        }
                    }
                }
                else // DMSTCALL - used status call
                {
                    Logger.WriteToLog("------- Running DMSTCALL -------", LogLevel.Verbose);
                    caseDataSet = GetListOfDMSTCasesSQL();
                    DataTable listOfCasesDS = caseDataSet.Tables[0];
                    foreach (DataRow row in listOfCasesDS.Rows)
                    {
                        // Need fileDate Logic    
                        // CaseID, CaseNbr, DtFile, NodeID, CurrCaseStatusCode, CalendarCode, CaseTypeCode, CaseSequence
                        // If less than 27 weeks from the filing week, then we want to schedule at least 27 weeks
                        if (DateTime.Parse(row[2].ToString()).AddDays(140) > DateTime.Today)
                        {
                            string hearingEventCode = "DRCSOSC"; // This is hardcoded due to time contraints.
                            bool eventAddSuccess = AddCaseEvent(row[0].ToString(), hearingEventCode);
                            
                            if (eventAddSuccess)
                            {
                                string eventCode = "DR4406"; // This is hardcoded due to time contraints.
                                bool eventAddSuccess2 = AddCaseEvent(row[0].ToString(), eventCode);
                                Logger.WriteToLog("Case Number " + row[1] + " processed successfully.", LogLevel.Basic);
                            }
                            else
                                Logger.WriteToLog("Case Number " + row[1] + " processed unsuccessfully.", LogLevel.Basic);
                        }
                        else // Logic to schedule hearing within file date timeframe.
                        {
                            string startDate = DateTime.Today.AddDays(140).ToString("MM/dd/yyyy");
                            string endDate = DateTime.Today.AddDays(161).ToString("MM/dd/yyyy");
                            string hearingType = "DRSTATUS";

                            // Find Hearing
                            string result = FindHearing(row, startDate, endDate, hearingType);
                            Logger.WriteToLog("Hearings" + result, LogLevel.Verbose);
                            if (result != "ERROR")
                            {
                                // If hearing is found, then spin through results to find the first available hearing.
                                string sessionID = FindFirstOpenSession(result);
                                ScheduleHearing(row, startDate, endDate, hearingType, sessionID);
                                Logger.WriteToLog("Case Number " + row[1] + " processed successfully.", LogLevel.Basic);
                            }
                            else
                                Logger.WriteToLog("Case Number " + row[1] + " processed unsuccessfully.", LogLevel.Basic);
                        }
                    }
                }

                // Identify Cases that haven't had any disposition codes per input parameters.
                
                
                
                // Of those cases, try to apply the event to schedule on a status call.
                //foreach(string caseID in casesList)
                //{
                //    AddCaseEvent(caseID);
                //}

                //string caseID = FindCase();

                Logger.WriteToLog("End no error.", LogLevel.Verbose);

            }
            catch (Exception e)
            {
                Context.Errors.Add(new BaseCustomException(e.Message));
            }

            // TODO: Handle errors we've collected during the job run.
            if (Context.Errors.Count > 0)
            {
                // Add a message to the job indicating that something went wrong.
                AddInformationToJob();

                // Collect errors, write them to a file, and attach the file to the job.
                LogErrors();
            }

            ContinueWithProcessing("Job Completed Successfully");
        }

        // Call with a single API
        /*
        private string FindCase()
        {
            Tyler.Odyssey.API.JobTemplate.FindCaseByCaseNumberEntity entity = new Tyler.Odyssey.API.JobTemplate.FindCaseByCaseNumberEntity();
            entity.SetStandardAttributes(800, "FindCase", Context.UserID, "FindCase", Context.SiteID);
            entity.CaseNumber = Context.Parameters.CaseNumber;

            OdysseyMessage msg = new OdysseyMessage(entity.ToOdysseyMessageXml(), Context.SiteID);
            string test = entity.ToOdysseyMessageXml();
            MessageHandlerFactory.Instance.ProcessMessage(msg);

            StringReader reader = new StringReader(msg.ResponseDocument.OuterXml);
            XmlSerializer serializer = new XmlSerializer(typeof(Tyler.Odyssey.API.JobTemplate.FindCaseByCaseNumberResultEntity));
            Tyler.Odyssey.API.JobTemplate.FindCaseByCaseNumberResultEntity result = (Tyler.Odyssey.API.JobTemplate.FindCaseByCaseNumberResultEntity)serializer.Deserialize(reader);

            return result.CaseID;
        }
        */
        // Call with API Transaction
        private bool AddCaseEvent(string caseID, string eventCode)
        {
            Tyler.Odyssey.API.JobTemplate.AddCaseEventEntity entity = new Tyler.Odyssey.API.JobTemplate.AddCaseEventEntity();
            entity.SetStandardAttributes(800, "AddEvent", Context.UserID, "AddEvent", Context.SiteID);
            entity.CaseID = caseID;
            entity.CaseEventType = eventCode;
            entity.Date = DateTime.Today.ToShortDateString();            

            TransactionEntity txn = new TransactionEntity();
            txn.TransactionType = "TylerAPIJobAddCaseEvent";
            txn.Messages.Add(entity);

            string responseXML = ProcessTransaction(txn.ToOdysseyTransactionXML());

            if (responseXML != "ERROR")
                return true;
            else
                return false;
        }

        private string FindHearing(DataRow row, string startDate, string endDate, string hearingType)
        {
            // Row Results
            // CaseID, CaseNbr, DtFile, NodeID, CurrCaseStatusCode, CalendarCode, CaseTypeCode, CaseSequence
            string caseID = row[0].ToString();
            string caseNbr = row[1].ToString();
            string judicialOfficer = row[5].ToString();            
            string nodeID = row[3].ToString();

            TransactionEntity txn = new TransactionEntity();
            txn.TransactionType = "ILCookLawDivisionCallJob";
            txn.ReferenceNumber = "ILCookLawDivisionCallJob";
            txn.Source = "ILCookLawDivisionCallJob";

            // **** Find Court Session **** //
            Entities.FindCourtSessionEntity findCourtSessionEntity = new Entities.FindCourtSessionEntity();
            // Message Attributes
            findCourtSessionEntity.SetStandardAttributes(1, "FindCourtSession", Context.UserID, "FindCourtSession", Context.SiteID);
            findCourtSessionEntity.ReferenceNumber = "ILCookLawDivisionCallJob";
            findCourtSessionEntity.Source = "ILCookLawDivisionCallJob";
            findCourtSessionEntity.NodeID = "0";
            findCourtSessionEntity.UserID = "1";

            /* Options */
            findCourtSessionEntity.Options = new Entities.FINDCOURTSESSIONOPTIONS();
            findCourtSessionEntity.Options.MaxNumberOfResults = "20";
            findCourtSessionEntity.Options.IncludeAdHocHearings = "false";

            // Search Nodes
            findCourtSessionEntity.Options.Nodes = new Entities.FINDCOURTSESSIONOPTIONSNODES();
            Entities.FINDCOURTSESSIONOPTIONSNODESLIST nodeList = new Entities.FINDCOURTSESSIONOPTIONSNODESLIST();
            nodeList.SearchNodeID = new string[1];
            nodeList.SearchNodeID[0] = nodeID;
            findCourtSessionEntity.Options.Nodes.Item = nodeList;

            /* Search Criteria */
            findCourtSessionEntity.SearchCriteria = new Entities.FINDCOURTSESSIONSEARCHCRITERIA();
            // Hearing Type
            findCourtSessionEntity.SearchCriteria.HearingTypes = new string[1];
            findCourtSessionEntity.SearchCriteria.HearingTypes[0] = hearingType;

            // Judge Resource
            findCourtSessionEntity.SearchCriteria.Resources = new Entities.FINDCOURTSESSIONSEARCHCRITERIARESOURCES();
            Entities.FINDCOURTSESSIONSEARCHCRITERIARESOURCEGROUPRESOURCE judicialOfficerEntity = new Entities.FINDCOURTSESSIONSEARCHCRITERIARESOURCEGROUPRESOURCE();
            judicialOfficerEntity.Resource = judicialOfficer;
            findCourtSessionEntity.SearchCriteria.Resources.JudicialOfficer = new Entities.FINDCOURTSESSIONSEARCHCRITERIARESOURCEGROUPRESOURCE[1];
            findCourtSessionEntity.SearchCriteria.Resources.JudicialOfficer[0] = judicialOfficerEntity;

            // Date Window
            findCourtSessionEntity.SearchCriteria.StartDate = startDate;
            findCourtSessionEntity.SearchCriteria.EndDate = endDate;

            txn.Messages.Add(findCourtSessionEntity);
            
            Logger.WriteToLog("Schedule Hearing Transaction Request: " + txn.ToOdysseyTransactionXML(), LogLevel.Verbose);
            string result = "";
            try
            {
                result = ProcessTransaction(txn.ToOdysseyTransactionXML());
                if (result != "ERROR")
                    return result;
                else
                {
                    Logger.WriteToLog("Cannot schedule a hearing.", LogLevel.Verbose);
                    return "ERROR";
                }
            }
            catch (Exception e)
            {
                Logger.WriteToLog("Cannot find a hearing. Exception: " + e, LogLevel.Verbose);
                return "ERROR";
            }
        }

        private bool ScheduleHearing(DataRow row, string startDate, string endDate, string hearingType, string sessionID)
        {
            // Row Results
            // CaseID, CaseNbr, DtFile, NodeID, CurrCaseStatusCode, CalendarCode, CaseTypeCode, CaseSequence
            string caseID = row[0].ToString();
            string caseNbr = row[1].ToString();
            string judicialOfficer = row[5].ToString();
            string nodeID = row[3].ToString();

            TransactionEntity txn = new TransactionEntity();
            txn.TransactionType = "ILCookLawDivisionCallJob";
            txn.ReferenceNumber = "ILCookLawDivisionCallJob";
            txn.Source = "ILCookLawDivisionCallJob";

            // Data Propagation            
            txn.DataPropagation.AddIntraTxnDataPropagationEntry("#|CourtSessionBlockID|#"
                , "/TxnResponse/Result[@MessageType='LoadCourtSession']/SessionBlocks/SessionBlock/SessionBlockID");

           
            // **** Load Court Session **** //
            Entities.LoadCourtSessionEntity loadCourtSessionEntity = new Entities.LoadCourtSessionEntity();
            loadCourtSessionEntity.SetStandardAttributes(1, "LoadCourtSession", Context.UserID, "LoadCourtSession", Context.SiteID);
            loadCourtSessionEntity.NodeID = nodeID;
            loadCourtSessionEntity.ReferenceNumber = "LoadCourtSession";
            loadCourtSessionEntity.UserID = "1";
            loadCourtSessionEntity.Source = "ILCookLawDivisionCallJob";
            loadCourtSessionEntity.SessionID = sessionID;
            txn.Messages.Add(loadCourtSessionEntity);


            // **** Add Hearing **** //
            Entities.AddHearingEntity addHearingEntity = new Entities.AddHearingEntity();
            addHearingEntity.SetStandardAttributes(1, "LoadCourtSession", Context.UserID, "LoadCourtSession", Context.SiteID);
            addHearingEntity.NodeID = nodeID;
            addHearingEntity.ReferenceNumber = "LoadCourtSession";
            addHearingEntity.UserID = "1";
            addHearingEntity.Source = "ILCookLawDivisionCallJob";

            addHearingEntity.CaseID = caseID;
            addHearingEntity.CourtSessionBlockID = "#|CourtSessionBlockID|#";
            addHearingEntity.HearingType = hearingType;
            addHearingEntity.OffSetMinutes = "0";
            addHearingEntity.HearingDuration = "30";
            addHearingEntity.HearingPriority = "1";

            Entities.JusticeCaseHearingInterpreterRequired interpreterRequired = new Entities.JusticeCaseHearingInterpreterRequired();
            addHearingEntity.InterpreterNeeded = new Entities.JusticeCaseHearingInterpreter();
            object notRequired = new object();
            addHearingEntity.InterpreterNeeded.Item = notRequired;

            txn.Messages.Add(addHearingEntity);

            string test = txn.ToOdysseyTransactionXML();
            Logger.WriteToLog("Schedule Hearing Transaction Request: " + txn.ToOdysseyTransactionXML(), LogLevel.Verbose);
            string result = "";
            try
            {
                result = ProcessTransaction(txn.ToOdysseyTransactionXML());
                if (result != "ERROR")
                    return true;
                else
                {
                    Logger.WriteToLog("Cannot schedule a hearing.", LogLevel.Verbose);
                    return false;
                }
            }
            catch(Exception e)
            {
                Logger.WriteToLog("Cannot find a hearing. Exception: " + e, LogLevel.Verbose);
                return false;
            }            
        }

        // Process Transaction
        public string ProcessTransaction(string transXml)
        {
            string txnResults = string.Empty;

            try
            {
                OdysseyTransaction txn = new OdysseyTransaction(0, transXml, Context.SiteID);
                TransactionProcessor txnProcessor = new TransactionProcessor();
                txnProcessor.ProcessTransaction(txn);

                if (txn.TransactionRejected)
                    throw new Exception(txn.RejectReason);
                else
                  if (txn.ResponseDocument != null)
                    txnResults = txn.ResponseDocument.OuterXml;
            }
            // if a schema exceptions is thrown, then throw the new exception with the data from the schema error.
            catch (SchemaValidationException svex)
            {
                throw new Exception(svex.ReplacementStrings[0]);
            }
            catch (DataConversionException dcex)
            {
                // check for an xslCodeQuery exception type in the inner exception
                // so we can report a better, more descriptive error 
                if (dcex.InnerException.GetType().Equals(typeof(XslCodeQueryException)))
                {
                    XslCodeQueryException xcqe = (XslCodeQueryException)dcex.InnerException;
                    throw new Exception(xcqe.ReplacementStrings[0], dcex);
                }
                else
                {
                    throw new Exception(dcex.Message);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteToLog("Transaction API Error: " + ex, LogLevel.Verbose);
                return "ERROR";
                //throw ex;
            }

            return txnResults;
        }

        public string FindFirstOpenSession(string result)
        {
            Logger.WriteToLog("Finding First Open Session.", LogLevel.Verbose);

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(result);

            XmlNodeList sessions = doc.SelectNodes("/TxnResponse/Result/Sessions/Session");

            foreach (XmlNode session in sessions)
            {
                Logger.WriteToLog("SessionID: " + session.SelectSingleNode("SessionID").InnerText + " is " 
                    + session.SelectSingleNode("Status").InnerText, LogLevel.Verbose);
                if (session.SelectSingleNode("Status").InnerText == "Open")
                    return session.SelectSingleNode("SessionID").InnerText;
            }

            return "0";
        }

        private void AddInformationToJob()
        {
            int jobTaskID = 0;
            int jobProcessID = 0;

            if (Int32.TryParse(Context.taskID, out jobTaskID) && Int32.TryParse(Context.jobProcessID, out jobProcessID))
            {
                object Parms = new object[,] { { "SEVERITY" }, { "2" } };

                ITYLJobTaskUtility taskUtility = (JobProcessingInterface.ITYLJobTaskUtility)Activator.CreateInstance(Type.GetTypeFromProgID("Tyler.Odyssey.JobProcessing.TYLJobTaskUtility.cTask"));

                taskUtility.AddTextMessage(Context.SiteID, jobProcessID, jobTaskID, "The job completed successfully, but some cases were not processed. Please see the attached error file for a list of those cases and the errors associated with each. A list manager list containing the cases in error was also created.", ref Parms);
            }
        }

        // SQL Query
        public DataSet GetListOfDMEXCasesSQL()
        {
            
            DataSet ds = null;
            string siteID = Context.SiteID;           

            // CaseID, CaseNbr, DtFile, NodeID, CurrCaseStatusCode, CalendarCode, CaseTypeCode, CaseSequence
            string query =
                "DECLARE @SearchDate DATETIME = GETDATE()" + " " +
                //"DECLARE @SearchDate DATETIME = '05/09/2020'" + " " +
                "DECLARE @SearchDateEnd DATETIME = DATEADD(week, -15, @SearchDate)" + " " +
                "DECLARE @SearchDateBeg DATETIME = DATEADD(day, -6, @SearchDateEnd)" + " " +
                "/* File Date Logic */" + " " +
                "DECLARE @SearchDateBegMM VARCHAR(2) = FORMAT(@SearchDateBeg, 'MM')" + " " +
                "DECLARE @SearchDateBegDD INT = FORMAT(@SearchDateBeg, 'dd')" + " " +
                "" + " " +
                "DECLARE @SearchDateEndMM VARCHAR(2) = FORMAT(@SearchDateEnd, 'MM')" + " " +
                "DECLARE @SearchDateEndDD INT = FORMAT(@SearchDateEnd, 'dd')" + " " +
                "" + " " +
                "SELECT" + " " +
                //"SELECT TOP 10" + " " +
                "CAH.CaseID, CAH.CaseNbr, CCH.DtFile, CAH.NodeID" + " " +
                ", UCStat.Code as 'CurrCaseStatusCode', UCJudge.Code as 'CalendarCode', UCCType.Code as 'CaseTypeCode'," + " " +
                "SUBSTRING(CAH.CaseNbr, LEN(CAH.CaseNbr) - 4, 5) as 'CaseSequence'" + " " +
                "FROM Justice.dbo.CaseAssignHist CAH with(nolock)" + " " +
                "JOIN Justice.dbo.ClkCaseHdr CCH with(nolock) ON CCH.CaseAssignmentHistoryIDCur = CAH.CaseAssignmentHistoryID" + " " +
                "/* Look 15 weeks back regardless of year */" + " " +
                "AND" + " " +
                "(" + " " +
                "    (" + " " +
                "        @SearchDateBegDD <= @SearchDateEndDD AND FORMAT(CCH.DtFile, 'dd') BETWEEN @SearchDateBegDD AND @SearchDateEndDD" + " " +
                "" + " " +
                "            AND FORMAT(CCH.DtFile, 'MM') IN(@SearchDateBegMM, @SearchDateEndMM)" + " " +
                "    )" + " " +
                "" + " " +
                "    OR" + " " +
                "    (" + " " +
                "        @SearchDateBegDD > @SearchDateEndDD" + " " +
                "" + " " +
                "        AND" + " " +
                "        (" + " " +
                "            FORMAT(CCH.DtFile, 'MM') IN(@SearchDateBegMM) AND" + " " +
                "            FORMAT(CCH.DtFile, 'dd') BETWEEN @SearchDateBegDD AND FORMAT(EOMONTH(@SearchDateBeg), 'dd')" + " " +
                "            OR" + " " +
                "" + " " +
                "            FORMAT(CCH.DtFile, 'MM') IN(@SearchDateEndMM) AND" + " " +
                "            FORMAT(CCH.DtFile, 'dd') BETWEEN FORMAT(DATEADD(DAY, 1, EOMONTH(@SearchDateBeg)), 'dd') AND @SearchDateEndDD" + " " +
                "        )" + " " +
                "    )" + " " +
                ")" + " " +
                "JOIN Justice.dbo.CaseStatusHist CSH with(nolock) ON CCH.StatusIDCur = CSH.StatusID" + " " +
                "JOIN Justice.dbo.ucode UCStat with(nolock) ON UCStat.CodeID = CSH.CaseStatClkCdID" + " " +
                "" + " " +
                "    AND UCStat.Code NOT IN('DRDISPOSED', 'DRINACTIVE', 'DRCLOSED')" + " " +
                "JOIN Justice.dbo.ucode UCCType with(nolock) ON UCCType.CodeID = CCH.CaseUTypeID" + " " +
                "JOIN Justice.dbo.ucode UCJudge with(nolock) ON UCJudge.CodeID = CAH.JudgeID" + " " +
                "" + " " +
                "WHERE" + " " +
                "CAH.NodeID BETWEEN 800 AND 860" + " " +
                "AND(" + " " +
                "        SUBSTRING(CAH.CaseNbr, LEN(CAH.CaseNbr) - 4, 5) >= '50000'" + " " +
                "        OR UCCType.Code IN('DR0033', 'DR0034', 'DR0035', 'DR0036', 'DR0037', 'DR0038', 'DR0039'" + " " +
                "        , 'DR0040', 'DR0041', 'DR0053', 'DR0054', 'DR0085')" + " " +
                "    )" + " " +
                "AND" + " " +
                "    NOT EXISTS(" + " " +
                "    SELECT TOP 1 1" + " " +
                "    FROM Justice.dbo.JudgmentEvent JE with(nolock)" + " " +
                "    Join Justice.dbo.Event EJud with(nolock) ON JE.JudgmentEventID = EJud.EventID AND EJud.Deleted <> 1" + " " +
                "    WHERE JE.CaseID = CAH.CaseID" + " " +
                "       )" + " " +
                "AND UCJudge.Code IN(" + " " +
                "        'DRCAL11','DRCAL21','DRCAL22','DRCAL23','DRCAL24'" + " " +
                "		,'DRCAL61','DRCAL62','DRCAL63','DRCAL64','DRCAL89'" + " " +
                "		,'DRCAL94','DRCAL95','DRCAL97','DRCAL98','DRCAL99'" + " " +
                "		,'DRCALCM','DRCALDM','DRCALEM','DRCAL0CP','DRCAL0DP','DRCAL0EP')" + " " +
                "order by CCH.DtFile, CAH.CaseNbr";

            try
            {
                Logger.WriteToLog("SQL: " + query, LogLevel.Verbose);

                ds = GetSqlDataSet(siteID, query);
                return ds;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public DataSet GetListOfDMSTCasesSQL()
        {

            DataSet ds = null;
            string siteID = Context.SiteID;

            string query =
                //"DECLARE @SearchDate DATETIME = GETDATE()" + " " +
                "DECLARE @SearchDate DATETIME = '04/21/2020'" + " " +
                "DECLARE @SearchDateEnd DATETIME = DATEADD(week, -17, @SearchDate)" + " " +
                "DECLARE @SearchDateBeg DATETIME = DATEADD(day, -6, @SearchDateEnd)" + " " +
                "" + " " +
                "/* File Date Logic */" + " " +
                "DECLARE @SearchDateBegMM VARCHAR(2) = FORMAT(@SearchDateBeg, 'MM')" + " " +
                "DECLARE @SearchDateBegDD INT = FORMAT(@SearchDateBeg, 'dd')" + " " +
                "DECLARE @SearchDateEndMM VARCHAR(2) = FORMAT(@SearchDateEnd, 'MM')" + " " +
                "DECLARE @SearchDateEndDD INT = FORMAT(@SearchDateEnd, 'dd')" + " " +
                "SELECT" + " " +
                //"SELECT TOP 1" + " " +
                "CAH.CaseID, CAH.CaseNbr, CCH.DtFile, CAH.NodeID" + " " +
                ", UCStat.Code as 'CurrCaseStatusCode', UCJudge.Code as 'CalendarCode', UCCType.Code as 'CaseTypeCode'," + " " +
                "SUBSTRING(CAH.CaseNbr, LEN(CAH.CaseNbr) - 4, 5) as 'CaseSequence'" + " " +
                "FROM Justice.dbo.CaseAssignHist CAH with(nolock)" + " " +
                "JOIN Justice.dbo.ClkCaseHdr CCH with(nolock) ON CCH.CaseAssignmentHistoryIDCur = CAH.CaseAssignmentHistoryID" + " " +
                "/* Look 15 weeks back regardless of year */" + " " +
                "AND" + " " +
                "(" + " " +
                "    (" + " " +
                "        @SearchDateBegDD <= @SearchDateEndDD AND FORMAT(CCH.DtFile, 'MM') BETWEEN @SearchDateBegDD AND @SearchDateEndDD" + " " +
                "            AND FORMAT(CCH.DtFile, 'MM') IN(@SearchDateBegMM, @SearchDateEndMM)" + " " +
                "    )" + " " +
                "    OR" + " " +
                "    (" + " " +
                "        @SearchDateBegDD > @SearchDateEndDD" + " " +
                "        AND" + " " +
                "        (" + " " +
                "            FORMAT(CCH.DtFile, 'MM') IN(@SearchDateBegMM) AND" + " " +
                "            FORMAT(CCH.DtFile, 'dd') BETWEEN @SearchDateBegDD AND FORMAT(EOMONTH(@SearchDateBeg), 'dd')" + " " +
                "            OR" + " " +
                "            FORMAT(CCH.DtFile, 'MM') IN(@SearchDateEndMM) AND" + " " +
                "            FORMAT(CCH.DtFile, 'dd') BETWEEN FORMAT(DATEADD(DAY, 1, EOMONTH(@SearchDateBeg)), 'dd') AND @SearchDateEndDD" + " " +
                "        )" + " " +
                "    )" + " " +
                ")" + " " +
                "JOIN Justice.dbo.CaseStatusHist CSH with(nolock) ON CCH.StatusIDCur = CSH.StatusID" + " " +
                "JOIN Justice.dbo.ucode UCStat with(nolock) ON UCStat.CodeID = CSH.CaseStatClkCdID" + " " +
                "    AND UCStat.Code NOT IN('DRDISPOSED', 'DRINACTIVE', 'DRCLOSED')" + " " +
                "JOIN Justice.dbo.ucode UCCType with(nolock) ON UCCType.CodeID = CCH.CaseUTypeID" + " " +
                "JOIN Justice.dbo.ucode UCJudge with(nolock) ON UCJudge.CodeID = CAH.JudgeID" + " " +
                "WHERE" + " " +
                "CAH.NodeID BETWEEN 800 AND 860" + " " +
                "AND(" + " " +
                "        SUBSTRING(CAH.CaseNbr, LEN(CAH.CaseNbr) - 4, 5) < '50000'" + " " +
                "        OR UCCType.Code NOT IN('DR0033', 'DR0034', 'DR0035', 'DR0036', 'DR0037', 'DR0038', 'DR0039'" + " " +
                "        , 'DR0040', 'DR0041', 'DR0053', 'DR0054', 'DR0085')" + " " +
                "    )" + " " +
                "AND" + " " +
                "    NOT EXISTS(" + " " +
                "    SELECT TOP 1 1" + " " +
                "" + " " +
                "    FROM Justice.dbo.JudgmentEvent JE with(nolock)" + " " +
                "    Join Justice.dbo.Event EJud with(nolock) ON JE.JudgmentEventID = EJud.EventID AND EJud.Deleted <> 1" + " " +
                "    WHERE JE.CaseID = CAH.CaseID" + " " +
                "       )" + " " +
                "AND UCJudge.Code IN(" + " " +
                "         'DRCAL11','DRCAL21','DRCAL22','DRCAL23','DRCAL24'" + " " +
                "		,'DRCAL61','DRCAL62','DRCAL63','DRCAL64','DRCAL89'" + " " +
                "		,'DRCAL94','DRCAL95','DRCAL97','DRCAL98','DRCAL99'" + " " +
                "		,'DRCALCM','DRCALDM','DRCALEM','DRCALGM','DRCALHM'" + " " +
                "		,'DRCALWM','DRCALXM','DRCALYM','DRCALZM')" + " " +
                "order by CCH.DtFile, CAH.CaseNbr";

            try
            {
                Logger.WriteToLog("SQL: " + query, LogLevel.Verbose);

                ds = GetSqlDataSet(siteID, query);
                return ds;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private DataSet GetSqlDataSet(string siteID, string query)
        {
            //string QUERY = createSQL(caseNumber);            
            CDBBroker broker = new CDBBroker(siteID);
            var brokerConnection = broker.GetConnection("Justice");
            DataSet ret = null;
            SqlCommand cmd = new SqlCommand(string.Format(query), brokerConnection as SqlConnection);

            try
            {
                cmd.Connection.Open();
                Logger.WriteToLog("Finding Cases", LogLevel.Verbose);
                ret = CDBBroker.LoadDataSet(siteID, cmd, true);
                Logger.WriteToLog("DataSet Record Count: " + ret.Tables[0].Rows.Count, LogLevel.Verbose);
            }
            finally
            {
                if (cmd != null)
                {
                    cmd.Connection.Close();
                }
            }

            return ret;

        }

        // Helper Methods
        private List<string> ExtractToStringList(DataSet sqlDataSet)
        {
            List<string> stringList = new List<string>();
            DataTable dt = sqlDataSet.Tables[0];

            foreach(DataRow row in dt.Rows)
            {
                //Logger.WriteToLog(row[0].ToString(), LogLevel.Verbose); // TEST REMOVE
                stringList.Add(row[0].ToString());
            }

            return stringList;                       
        }



        private void LogErrors()
        {
            using (StreamWriter writer = GetTempFile())
            {
                Logger.WriteToLog("Beginning to write to temp file.", LogLevel.Intermediate);

                // Write the file header
                writer.WriteLine("CaseNumber,CaseID,CaseFlag,Error");

                // For each error, write some information.
                Context.Errors.ForEach((BaseCustomException f) => WriteErrorToLog(f, writer));

                Logger.WriteToLog("Finished writing to temp file.", LogLevel.Intermediate);

                AttachTempFileToJobOutput(writer, @"Add Remove Case Flags Action - Errors");
            }
        }


        private void WriteErrorToLog(BaseCustomException exception, StreamWriter writer)
        {
            writer.WriteLine(string.Format("\"{0}\"", exception.CustomMessage));
        }


        private StreamWriter GetTempFile()
        {
            if (TaskUtility == null)
                return null;

            string filePath = TaskUtility.GenerateFile(Context.SiteID, ref taskParms);
            StreamWriter fileWriter = new StreamWriter(filePath, true);

            Logger.WriteToLog("Created temp file at location: " + filePath, LogLevel.Basic);

            return fileWriter;
        }


        private void AttachTempFileToJobOutput(StreamWriter writer, string errorFileName)
        {
            Logger.WriteToLog("Begining AttachTempFileToJobOutput()", LogLevel.Intermediate);
            Logger.WriteToLog(writer == null ? "File is NULL" : "File is NOT NULL", LogLevel.Intermediate);

            if (writer != null && TaskUtility != null)
            {
                FileStream fileStream = writer.BaseStream as FileStream;
                string filePath = fileStream.Name;
                Logger.WriteToLog("File Path: " + filePath, LogLevel.Intermediate);

                writer.Close();

                if (filePath.Length > 0 && errorFileName.Length > 0)
                    AttachFile(filePath, errorFileName);

                Logger.WriteToLog("Completed AttachTempFileToJobOutput()", LogLevel.Intermediate);
            }
        }


        private void AttachFile(string filepath, string filename)
        {
            DataProcessor.Logger.WriteToLog("Begin AttachFile()", Tyler.Odyssey.Utils.LogLevel.Intermediate);
            int nodeID = 0;
            int taskIDInt = 0;
            int jobProcessIDInt = 0;

            if (TaskUtility != null)
            {
                if (Int32.TryParse(Context.taskID, out taskIDInt) && Int32.TryParse(Context.jobProcessID, out jobProcessIDInt))
                {
                    int documentID = TaskUtility.AddOutputDocument(this.siteKey, taskIDInt, jobProcessIDInt, -1, filepath, Context.UserID, nodeID, ref taskParms);

                    if (documentID > 0)
                    {
                        TaskUtility.AddOutputParams(this.siteKey, taskIDInt, "TEXT", documentID, filename, TaskDocument, ref taskParms);

                        TaskUtility.DeleteTempFile(filepath);

                        this.OutputJobTaskXML = TaskDocument.documentElement.xml;
                    }
                }
            }

            DataProcessor.Logger.WriteToLog("End Attach()", Tyler.Odyssey.Utils.LogLevel.Intermediate);
        }
    }
}